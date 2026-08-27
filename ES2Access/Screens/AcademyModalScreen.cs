using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The Academy's own window - what the Academy diplomacy window switches to, and what four of the
    /// game's notifications open: the requests the Academy is making of the player, the role it has given
    /// them, the reparations it wants, and the named ships it has lent out.
    ///
    /// Modelled BY VISIBILITY rather than by content. The window is a stack of fifteen separate panel
    /// classes instantiated from prefabs (<c>Load</c> :152-163), each deciding for itself whether it has
    /// anything to say this turn (<c>MustShow</c>) and the window arranging whichever answered yes
    /// (<c>RefreshPanelsVisibility</c> :343-357). So one stop per DRAWN panel, in the order the window
    /// arranged them, read by the shape of what each draws - which is the same treatment every side panel
    /// in this mod gets, and is what keeps fifteen classes from becoming fifteen readers.
    ///
    /// The named-ship strip is its own stop while it is up, because it is not one of those panels: the
    /// window instantiates it separately into its own container (:162).
    ///
    /// <b>One hazard is inherited, not solved.</b> A named ship's own panel can raise the system-selection
    /// picker (<c>NamedShipInfoPanel</c> :126-132), and that picker is modelled at a LOWER layer than this
    /// window (25 against 46). The exclusive modal stack is expected to withdraw this window while the
    /// picker is up - which is what the hero-selection window measured - and the mod's departure gate then
    /// hands the keyboard over cleanly. It could not be measured in the fixture this was built against,
    /// where the window cannot be bound at all, so it is stated as the expectation it is.
    ///
    /// Escape is the game's, and this window and the Academy's diplomacy window can never be up together:
    /// each switch hides itself before showing the other, which is why they share a layer.
    /// </summary>
    public sealed class AcademyModalScreen : Screen
    {
        private static readonly object ShipsStop = "academy-modal:ships";

        private const string Keys = "academy-modal:";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.academy-modal"; }
        }

        /// <summary>Shared with the Academy's diplomacy window, which it swaps places with.</summary>
        public override int Layer
        {
            get { return 46; }
        }

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenAcademyModal); }
        }

        public override bool ConsumesBack
        {
            get { return false; }
        }

        public override bool IsActive()
        {
            try
            {
                AcademyModalWindow window = Window();
                return window != null
                    && window.Shown
                    && window.IsReady
                    && Panels(window) != null
                    && !Buried(window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Buried(GuiModalWindow window)
        {
            try
            {
                GuiManager manager = Gui.GuiGameWindowService as GuiManager;
                GuiModalWindow top = manager == null ? null : manager.ModalOnTop;
                return top != null && !ReferenceEquals(top, window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            AcademyModalWindow window = Window();
            IList<AgeTransform> panels = window == null ? null : Panels(window);
            if (panels == null)
            {
                return;
            }

            for (int i = 0; i < panels.Count; i++)
            {
                BuildPanel(builder, panels[i], i);
            }

            BuildShips(builder, window);
        }

        /// <summary>One panel the window is showing, as its own stop named by whatever heading it drew.
        /// </summary>
        private void BuildPanel(GraphBuilder builder, AgeTransform panel, int index)
        {
            if (panel == null || !AgeWidgets.Visible(panel))
            {
                return;
            }

            builder.BeginStop(Keys + "panel/" + panel.name);
            string title = AgeWidgets.TextOf(AgeWidgets.ChildNamed(panel, "Title", 3));
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            _cells.Clear();
            try
            {
                SidePanels.Content(_cells, panel, Keys + "panel/" + index + "/", null, null);
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading a panel threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The strip of named ships, while the window is showing it, and the button that switches
        /// to the Academy's diplomacy window. The strip's own stat block is the one thing in it the shape
        /// of the tree cannot read - see <see cref="NamedShipStats"/>.</summary>
        private void BuildShips(GraphBuilder builder, AcademyModalWindow window)
        {
            AgeTransform container = window.NamedShipGroupContainer;
            bool drawn = container != null && AgeWidgets.Visible(container);
            builder.BeginStop(ShipsStop);
            builder.PushContext(ModStrings.Get(ModStrings.AcademyNamedShips));
            _cells.Clear();
            try
            {
                if (drawn)
                {
                    SidePanels.Content(_cells, container, Keys + "ships/", NamedShipStats.Cell, null);
                }
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading the named ships threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            AcademyWindows.Switch(builder, window, Keys);
            builder.PopContext();
            WindowShape.Close(builder, window, Keys);
        }

        /// <summary>The panels the window has arranged, in the order it arranged them. Taken off the table
        /// the window parents them to rather than out of its own private list, which is the same trick the
        /// diplomacy ring's reading order uses.</summary>
        private static IList<AgeTransform> Panels(AcademyModalWindow window)
        {
            try
            {
                return window.panelsTable == null ? null : window.panelsTable.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AcademyModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<AcademyModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The stat block of the named ship the Academy is offering, read the way a ship's tooltip reads the
    /// same figures.
    ///
    /// <c>PanelFeatureNamedShipInfo</c> is the Academy's own copy of the ship stat block, and the shape
    /// walk cannot read it for two reasons. It is buried SEVEN groups under the window's named-ship
    /// container (the panel puts its features inside a scroll view, which is three more groups), one past
    /// the walk's depth limit, so the whole feature came out as a single line of every word in it run
    /// together - "Role Colonizer Size Small ... 60000/60000 ... 26000 13500". And half of what it draws
    /// is a bare number beside a picture: health, manpower, movement, command points and the two military
    /// powers carry no caption anywhere on the panel, and the two balance bars carry no text at all.
    ///
    /// So the feature answers for itself (<see cref="Cell"/>): the captionless figures are declared by
    /// FIELD under the game's own title for each statistic - the same <c>%ShipStat…Title</c> strings the
    /// ship designer's copy of these figures is read under - the two bars are declared as the split they
    /// draw (<see cref="BalanceGauges"/>), and the rest of the feature is then read by shape from the
    /// feature's OWN root, where the depth limit starts again from zero. The rows that already caption
    /// themselves - role, size, probes, cargo, and the "Projectile-Energy Balance" heading - come out of
    /// that walk unchanged, which is why they are not modelled here.
    ///
    /// The fighter and bomber counts are the one pair the game keeps no <c>%ShipStat…Title</c> for (the
    /// prefab points their tooltips at <c>%ShipStatFighterDescription</c>, which is not in the game's
    /// strings at all). They take the game's own words for the two squadron kinds instead - the titles it
    /// heads their module sub-categories with - because the alternative is two bare counts of 99.
    /// </summary>
    internal static class NamedShipStats
    {
        /// <summary>What the game calls the two kinds of squadron, where it names them as things rather
        /// than as a statistic of the ship carrying them.</summary>
        private const string FighterTitle = "%SubCategoryModuleSquadronFighterTitle";

        private const string BomberTitle = "%SubCategoryModuleSquadronBomberTitle";

        /// <summary>What the shape walk is to leave alone: the group each declared figure was drawn in,
        /// and the two bars. Refilled per read, main-thread only, like the ship designer's own.</summary>
        private static readonly List<AgeTransform> _declared = new List<AgeTransform>();

        /// <summary>The named-ship stat block's answer for itself, as
        /// <see cref="SidePanels.SpecialCells"/> asks it.</summary>
        public static bool Cell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            PanelFeatureNamedShipInfo ship = widget.GetComponent<PanelFeatureNamedShipInfo>();
            if (ship == null)
            {
                return false;
            }

            Read(cells, ship, keyPrefix);
            return true;
        }

        private static void Read(List<Cell> cells, PanelFeatureNamedShipInfo ship, string keyPrefix)
        {
            try
            {
                _declared.Clear();
                Stat(cells, ship.HealthLabel, "%ShipStatHealthTitle", keyPrefix + "health");
                Stat(cells, ship.ManpowerLabel, "%ShipStatManpowerTitle", keyPrefix + "manpower");
                Stat(cells, ship.MovementPointsLabel, "%ShipStatMovementTitle", keyPrefix + "movement");
                Stat(
                    cells,
                    ship.CommandPointsLabel,
                    "%ShipStatCommandPointsTitle",
                    keyPrefix + "command-points"
                );
                Stat(
                    cells,
                    ship.OffensivePowerLabel,
                    "%ShipStatOffensiveMilitaryPowerTitle",
                    keyPrefix + "offence"
                );
                Stat(
                    cells,
                    ship.DefensivePowerLabel,
                    "%ShipStatDefensiveMilitaryPowerTitle",
                    keyPrefix + "defence"
                );
                Stat(cells, ship.FighterLabel, FighterTitle, keyPrefix + "fighters");
                Stat(cells, ship.BomberLabel, BomberTitle, keyPrefix + "bombers");
                Gauge(cells, ship.OffensiveBalanceGauge, keyPrefix + "offensive-balance");
                Gauge(cells, ship.DefensiveBalanceGauge, keyPrefix + "defensive-balance");
                SidePanels.Content(cells, ship.AgeTransform, keyPrefix, Declared, null);
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading the named ship's stats threw: " + e);
            }
            finally
            {
                _declared.Clear();
            }
        }

        private static void Stat(
            List<Cell> cells,
            AgePrimitiveLabel label,
            string titleKey,
            string key
        )
        {
            AgeTransform at = label == null ? null : label.AgeTransform;
            if (at == null)
            {
                return;
            }

            Cells.AddStat(cells, label, titleKey, key);
            _declared.Add(at.Parent ?? at);
        }

        private static void Gauge(
            List<Cell> cells,
            RepartitionHorizontalGauge gauge,
            string key
        )
        {
            if (gauge == null)
            {
                return;
            }

            BalanceGauges.Add(cells, gauge, key);
            _declared.Add(gauge.AgeTransform);
        }

        private static bool Declared(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            return _declared.Contains(widget);
        }
    }

    /// <summary>The button both Academy windows draw to swap places with each other. Neither window
    /// exposes it as a field, so it is found by the handler the prefab wires it to - which is also the only
    /// thing that identifies it, since it is drawn as a bare icon.</summary>
    internal static class AcademyWindows
    {
        private const string SwitchHandler = "OnSwitchWindowsCb";

        public static void Switch(GraphBuilder builder, GuiWindow window, string keyPrefix)
        {
            try
            {
                AgeControlButton button = Button(window);
                AgeTransform at = AgeWidgets.Transform(button);
                if (at == null || !AgeWidgets.Visible(at))
                {
                    return;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(at);
                NodeVtable vtable = GraphNodes.Button(
                    CardActions.NameFromTooltip(tooltip),
                    () => AgeWidgets.Press(at),
                    () => AgeWidgets.Offered(at),
                    tooltip,
                    TooltipMode.None
                );
                AgeWidgets.PointAt(vtable, at);
                builder.AddItem(Nodes.Drawn(ControlId.For(at, keyPrefix + "switch"), vtable, at));
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading the switch button threw: " + e);
            }
        }

        private static AgeControlButton Button(GuiWindow window)
        {
            AgeControlButton[] buttons = window.AgeTransform.GetComponentsInChildren<AgeControlButton>(
                true
            );
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].OnActivateMethod == SwitchHandler)
                {
                    return buttons[i];
                }
            }

            return null;
        }
    }
}

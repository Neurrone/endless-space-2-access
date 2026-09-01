using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// THE HACKING FAMILY - the four panels the scan overlay draws that belong to no lens.
    ///
    /// They are a DLC's (<c>ScanOverlayWindow</c> :242, <c>IsShared("DLCUC")</c>), and on an install
    /// without it the game hides all three hacking transforms outright and never even animates them
    /// with the layer. So everything here is read off DRAWN widgets and simply declares nothing on such
    /// an install - which is also why it needed no gate of its own: the gate is the picture.
    ///
    /// They are lens-INDEPENDENT: the same overlay window carries them at every galaxy lens and at the
    /// system-management rung, so they are declared beside the legend rather than under a lens, in the
    /// order the screen draws them down its left edge and across its top.
    ///
    /// TWO LABELS ARE EXCLUDED ON PURPOSE (measured with the family forced on, 2026-09-01): the trace
    /// banner's <c>TracingSpeedLabel</c> and <c>TraceOperationsLabel</c> draw the raw localization keys
    /// <c>%TracingSpeedTitle</c> / <c>%TraceOperationsCountTitle</c> - nothing ever refreshes them - and
    /// the traitors banner's <c>TotalRevenueLabel</c> carries prefab text ("Total siphon: 45...") the
    /// game hides. Reading any of the three would speak a number that is not about this game.
    /// </summary>
    public sealed partial class ScanLensPanels
    {
        /// <summary>The bandwidth and operations banner, top-left of the overlay.</summary>
        public static readonly object HackingStop = "scan:hacking";

        /// <summary>The sleeper banner under it.</summary>
        public static readonly object TraitorsStop = "scan:traitors";

        /// <summary>The console the player launches programs from.</summary>
        public static readonly object ConsoleStop = "scan:console";

        /// <summary>The chips the overlay stacks its own notifications in.</summary>
        public static readonly object ScanNotificationsStop = "scan:notifications";

        /// <summary>Everything the family draws, each panel a stop of its own and each declared only
        /// while its own transform is on the screen.</summary>
        public void Hacking(GraphBuilder builder)
        {
            ScanOverlayWindow window = Window<ScanOverlayWindow>();
            if (window == null || !window.Shown)
            {
                return;
            }

            try
            {
                HackingBanner(builder, window.HackingBanner);
                TraitorsBanner(builder, window.TraitorsBanner);
                Console(builder, window.HackingDashboard);
                ScanNotifications(builder, window.ScanNotificationPanel);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the hacking family threw: " + e);
            }
        }

        // ---- the hacking banner ----

        /// <summary>
        /// What the empire has to hack WITH and what it is doing with it: the bandwidth line and one
        /// row per allocation of it, the speed, the operations count and one row per running operation.
        ///
        /// The allocation cells and the operation lines are pooled tables that are empty in every
        /// fixture this project has - no operation has ever existed in one - so their per-row content
        /// is code-verified only, and the walk reads each row's own widgets rather than the model.
        /// </summary>
        private void HackingBanner(GraphBuilder builder, ScanViewWindowHackingBanner banner)
        {
            // Flow control: whether this panel is walked at all. The window keeps the component
            // whether or not the DLC turned it on, and hides the transform when it did not.
            if (banner == null || !AgeWidgets.Visible(banner.AgeTransform))
            {
                return;
            }

            builder.BeginStop(HackingStop);
            Readout(builder, "scan:hacking/bandwidth", banner.ProcessingPowerTitle,
                banner.ProcessingPowerTooltip);
            Allocations(builder, banner.AllocatedProcessingPowerCellTable);
            Drawn(builder, "scan:hacking/overcap", banner.ProcessingPowerOvercapWarning);
            Readout(builder, "scan:hacking/speed", banner.HackingSpeedLabel, null);
            Readout(builder, "scan:hacking/operations", banner.HackingOperationsLabel, null);
            Operations(builder, banner.HackingOperationLinesTable, "scan:hacking/operation/");
            // The trace group's own two labels are the placeholders named above; its LINES are real
            // rows of the same shape as the operations, so the group contributes those and nothing else.
            if (AgeWidgets.Painted(banner.TraceOperationsGroup))
            {
                Operations(builder, banner.TraceOperationLinesTable, "scan:hacking/trace/");
            }
        }

        /// <summary>One row per slice of bandwidth something has taken. The cell is a coloured bar with
        /// no words at all, so the row is its own class-backed dossier; the game's click on it goes to
        /// where the thing is on the map and its right click cancels the allocation, which is the
        /// contextual key here as it is everywhere.</summary>
        private static void Allocations(GraphBuilder builder, AgeTransform table)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AllocatedProcessingPowerCell cell =
                    children[i] == null ? null : children[i].GetComponent<AllocatedProcessingPowerCell>();
                // Flow control: whether this pooled cell is a row at all. The table reserves widgets
                // and hides the ones a smaller allocation set does not need (:22-27).
                if (cell == null || !AgeWidgets.Visible(cell.AgeTransform))
                {
                    continue;
                }

                AllocatedProcessingPowerCell it = cell;
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeWidgets.TooltipTitle(it.Tooltip),
                    () => AgeWidgets.Press(it.Button),
                    () => AgeWidgets.Operable(it.AgeTransform),
                    it.Tooltip
                );
                // The whole body of the cell's own right-click handler (:50-53), called rather than
                // simulated: the game exposes the act, so there is nothing to press.
                vtable.OnContextual = () => it.AllocationProvider.Cancel();
                AgeWidgets.Point(vtable, it.Button, it.Tooltip, it.AgeTransform);
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.For(cell, "scan:hacking/allocation/" + i),
                        vtable,
                        cell
                    )
                );
            }
        }

        /// <summary>One row per operation the banner is listing - where it starts, what it is aimed at
        /// and how long it has left, which the line draws as three labels and no sentence. Its tick is
        /// what draws the operation's path over the map, so the row IS that tick.</summary>
        private static void Operations(GraphBuilder builder, AgeTransform table, string head)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                HackingOperationLine line =
                    children[i] == null ? null : children[i].GetComponent<HackingOperationLine>();
                // Flow control: whether this pooled line is a row at all.
                if (line == null || !AgeWidgets.Visible(line.AgeTransform))
                {
                    continue;
                }

                HackingOperationLine it = line;
                AgeControlToggle toggle = line.Toggle;
                NodeVtable vtable = toggle == null
                    ? GraphBuilder.Label(() => OperationName(it))
                    : GraphNodes.Checkbox(
                        () => OperationName(it),
                        () => toggle.State,
                        () => AgeWidgets.Toggle(toggle),
                        () => AgeWidgets.Operable(it.AgeTransform),
                        AgeWidgets.Raw(it.AgeTransform)
                    );
                AgeWidgets.PointAt(vtable, it.AgeTransform);
                builder.AddItem(
                    Nodes.Drawn(ControlId.For(line, head + i), vtable, line)
                );
            }
        }

        /// <summary>An operation named the way the line draws it: the system it runs from, what it is
        /// aimed at, and the turns left where the line is showing them.</summary>
        private static string OperationName(HackingOperationLine line)
        {
            Core.Speech.MessageBuilder message = new Core.Speech.MessageBuilder();
            message.Fragment(AgeText.Label(line.StartNameLabel));
            message.Fragment(AgeText.Label(line.TargetNameLabel));
            // Content: the line hides the duration for a trace and for an operation with none left,
            // and the label keeps whatever it last said.
            if (
                line.RemainingDurationLabel != null
                && AgeWidgets.Painted(line.RemainingDurationLabel.AgeTransform)
            )
            {
                message.Fragment(AgeText.Label(line.RemainingDurationLabel));
            }

            return message.Build();
        }

        // ---- the traitors banner ----

        /// <summary>How many sleepers the empire is carrying, and - behind the game's own toggle - who
        /// planted them. The toggle is a real control here because that is what it is on the screen: it
        /// folds the per-empire table away, and the game switches it off with a failure sentence in its
        /// tooltip when there is nobody to list.</summary>
        private void TraitorsBanner(GraphBuilder builder, ScanViewWindowTraitorsBanner banner)
        {
            // Flow control: whether this panel is walked at all.
            if (banner == null || !AgeWidgets.Visible(banner.AgeTransform))
            {
                return;
            }

            builder.BeginStop(TraitorsStop);
            Readout(builder, "scan:traitors/count", banner.TotalCountLabel, null);

            AgeControlToggle toggle = banner.DetailsToggle;
            AgeTransform body = banner.ToggleBodyAgeTransform;
            if (toggle != null && body != null)
            {
                AgeControlToggle it = toggle;
                AgeTransform widget = body;
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => AgeWidgets.TextOf(widget),
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Operable(widget),
                    AgeWidgets.Raw(widget)
                );
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(
                    Nodes.Drawn(ControlId.For(toggle, "scan:traitors/details"), vtable, toggle)
                );
            }

            TraitorEmpires(builder, banner.TraitorsEmpireTable);
        }

        /// <summary>One row per empire with sleepers here. The item draws an emblem, a row of threshold
        /// circles and a locate button and no words of its own, so the row is named by whatever text
        /// the item is drawing, carries the empire dossier the game hangs on the emblem, and its Enter
        /// is the locate button - the one thing the item does.</summary>
        private static void TraitorEmpires(GraphBuilder builder, AgeTransform table)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                TraitorBannerEmpireItem item =
                    children[i] == null ? null : children[i].GetComponent<TraitorBannerEmpireItem>();
                // Flow control: whether this pooled item is a row at all.
                if (item == null || !AgeWidgets.Visible(item.AgeTransform))
                {
                    continue;
                }

                TraitorBannerEmpireItem it = item;
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeWidgets.TooltipTitle(it.EmpireTooltip),
                    () => AgeWidgets.Press(it.CycleLocationButton),
                    () => AgeWidgets.Operable(it.CycleLocationButton),
                    it.EmpireTooltip
                );
                AgeWidgets.PointAt(vtable, it.AgeTransform, it.EmpireTooltip);
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.For(item, "scan:traitors/empire/" + i),
                        vtable,
                        item
                    )
                );
            }
        }

        // ---- the console ----

        /// <summary>
        /// The console the player runs hacking from: three toggles - enter hacking mode, open the
        /// defensive menu, open the offensive menu - and, while a menu is open, the programs in it.
        ///
        /// The stop wears the console's own title, which the game rewrites per mode, so the player is
        /// told which mode they are in on the way in rather than having to find the toggle that is on.
        ///
        /// A program's Enter is the game's own click on its line, which arms the hacking targeting
        /// cursor - one of the nine the mod already models (<c>CursorTargeting</c>), so the map's Enter
        /// becomes that cursor's confirm and Backslash its cancel with nothing declared here.
        /// </summary>
        private void Console(GraphBuilder builder, ScanViewWindowHackingDashboard console)
        {
            // Flow control: whether this panel is walked at all.
            if (console == null || !AgeWidgets.Visible(console.AgeTransform))
            {
                return;
            }

            builder.BeginStop(ConsoleStop);
            ScanViewWindowHackingDashboard it = console;
            builder.PushContext(AgeText.Label(console.TitleLabel));
            Toggle(builder, "scan:console/mode", console.HackingOperationModeToggle, it, 0);
            Toggle(builder, "scan:console/defensive", console.DefensiveProgramMenuToggle, it, 1);
            Programs(builder, console.DefensiveProgramMenu, "scan:console/defensive/");
            Toggle(builder, "scan:console/offensive", console.OffensiveProgramMenuToggle, it, 2);
            Programs(builder, console.OffensiveProgramMenu, "scan:console/offensive/");
            builder.PopContext();
        }

        /// <summary>
        /// One of the console's three switches, keyed to the mode it selects: the game holds the states
        /// in an array parallel to the modes, and the widget the player clicks is the transform beside
        /// the tick, which is where the game also hangs the tooltip that says why it is switched off.
        ///
        /// The switch draws no text at all - it is an icon - so it is named with the game's own title
        /// for the mode it turns on (<c>%HackingDashboard&lt;Mode&gt;Title</c>, the very key the console
        /// re-titles ITSELF with while that mode is running, <c>Refresh</c> :135). Nothing here is the
        /// mod's word.
        /// </summary>
        private static void Toggle(
            GraphBuilder builder,
            string key,
            AgeTransform widget,
            ScanViewWindowHackingDashboard console,
            int mode
        )
        {
            AgeControlToggle[] toggles = console.Toggles;
            AgeControlToggle toggle =
                toggles == null || mode >= toggles.Length ? null : toggles[mode];
            // Flow control: whether this switch is declared at all.
            if (widget == null || toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform target = widget;
            ScanViewWindowHackingDashboard dashboard = console;
            int picked = mode;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModeTitle(picked),
                () => it.State,
                () => dashboard.OnToggleMode(
                    (ScanViewWindowHackingDashboard.HackingMode)picked
                ),
                () => AgeWidgets.Operable(target),
                AgeWidgets.Raw(target)
            );
            AgeWidgets.PointAt(vtable, target);
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, key), vtable, toggle));
        }

        /// <summary>The game's own title for a console mode, composed exactly as the console composes
        /// its own heading from the mode it is in.</summary>
        private static string ModeTitle(int mode)
        {
            try
            {
                string name = ((ScanViewWindowHackingDashboard.HackingMode)mode).ToString();
                return AgeText.Clean(Gui.Localize("%HackingDashboard" + name + "Title"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The programs in whichever menu is open. The menu is hidden until its toggle is on
        /// and the game reserves a line per program the empire may launch, so the walk is the drawn
        /// lines and the count comes with them.</summary>
        private static void Programs(GraphBuilder builder, GuiPanel menu, string head)
        {
            AgeTransform table = menu == null ? null : menu.AgeTransform;
            // Flow control: whether the menu is walked at all - the console hides it whenever its own
            // mode is not the active one.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> children = table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                HackingProgramLine line =
                    children[i] == null ? null : children[i].GetComponent<HackingProgramLine>();
                // Flow control: whether this pooled line is a row at all.
                if (line == null || !AgeWidgets.Visible(line.AgeTransform))
                {
                    continue;
                }

                HackingProgramLine it = line;
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(it.Label),
                    () => AgeWidgets.Press(it.Button),
                    () => AgeWidgets.Operable(it.AgeTransform),
                    AgeWidgets.Raw(it.AgeTransform)
                );
                AgeWidgets.Point(vtable, it.Button, AgeWidgets.Raw(it.AgeTransform), it.AgeTransform);
                builder.AddItem(Nodes.Drawn(ControlId.For(line, head + i), vtable, line));
            }
        }

        // ---- the overlay's own notifications ----

        /// <summary>The chips the overlay stacks down its side, one row each. The panel hides all but
        /// the first until a pointer is over it, and the mod declares whichever ones the panel is
        /// drawing - a chip nobody can see is not a row.</summary>
        private void ScanNotifications(GraphBuilder builder, ScanNotificationItemsPanel panel)
        {
            AgeTransform table = panel == null ? null : panel.NotificationItemsTable;
            // Flow control: whether the panel is walked at all.
            if (panel == null || !panel.Shown || table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> children = table.Children;
            int rows = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                ScanNotificationItem item =
                    children[i] == null ? null : children[i].GetComponent<ScanNotificationItem>();
                // Flow control: whether this pooled chip is a row at all.
                if (item == null || !AgeWidgets.Visible(item.AgeTransform))
                {
                    continue;
                }

                if (rows == 0)
                {
                    builder.BeginStop(ScanNotificationsStop);
                }

                ScanNotificationItem it = item;
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(it.Label),
                    () => AgeWidgets.Press(it.Button),
                    () => AgeWidgets.Operable(it.AgeTransform),
                    it.Tooltip
                );
                AgeWidgets.Point(vtable, it.Button, it.Tooltip, it.AgeTransform);
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.For(item, "scan:notification/" + i),
                        vtable,
                        item
                    )
                );
                rows++;
            }
        }

        // ---- shared shapes ----

        /// <summary>A figure the banner writes out as a sentence of its own - the bandwidth line, the
        /// speed, the operations count, the sleeper count. Declared only while the label is drawing, so
        /// a group the game folds away takes its rows with it.</summary>
        private static void Readout(
            GraphBuilder builder,
            string key,
            AgePrimitiveLabel label,
            AgeTooltip tooltip
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            // Flow control: whether this figure is a row at all.
            if (widget == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = GraphBuilder.Label(() => AgeText.Label(it), widget);
            if (tooltip != null)
            {
                vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(tooltip));
                AgeWidgets.PointAt(vtable, widget, tooltip);
            }

            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>A warning the banner puts up only when it applies, read as whatever it is drawing.
        /// </summary>
        private static void Drawn(GraphBuilder builder, string key, AgeTransform widget)
        {
            // Flow control: whether the warning is a row at all - the banner hides it whenever nothing
            // is over its cap.
            if (widget == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            AgeTransform it = widget;
            builder.AddItem(
                Nodes.Drawn(
                    ControlId.For(widget, key),
                    GraphBuilder.Label(() => AgeWidgets.TextOf(it), it),
                    widget
                )
            );
        }
    }
}

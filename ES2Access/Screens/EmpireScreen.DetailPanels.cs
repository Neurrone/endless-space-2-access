using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The panels a system cell slides out, and the watch that says which one the game has
    /// just opened or closed.</summary>
    public sealed partial class EmpireScreen
    {
        // ---- the panels a cell slides out ----

        /// <summary>What the table has under it - the four things a cell can slide out, as the player
        /// meets them. The construction cell shows the game's constructibles and queue panels side by
        /// side, which is one thing to press a cell for and so one member here. The status and
        /// population cells slide out the SAME cards panel in two drawn modes
        /// (<c>StarSystemsManagementPanel.OnLineSelection</c> :311-318 picks by clicked cell,
        /// <c>PlanetCard.DisplayMode</c>): actions, with the colonize/specialization/terraform buttons
        /// on each card, and population, with the rings and no buttons at all - so they are two members
        /// here, because a player told only "planets panel" cannot know which of two different panels
        /// arrived, or that pressing the OTHER cell is what brings out the buttons.</summary>
        private enum Detail
        {
            None,
            PlanetActions,
            PlanetPopulation,
            Construction,
            Hangar,
        }

        /// <summary>
        /// A panel arriving under the table, or going away, is announced - the same reason the fleet
        /// panel announces itself: the page does not change, so a whole Tab stop's worth of content
        /// appears and disappears under the player with nothing to hear but Tab taking longer to come
        /// round. Queued rather than interrupting: the player pressed the cell, and cutting off the
        /// cell's own readout would take away the answer they asked for.
        ///
        /// A SWAP - another cell, or the same cell on another row - says only the new opening. The
        /// panels change over in one frame (<c>StarSystemsManagementPanel.OnLineSelection</c> :285-311
        /// hides all of them and shows the one the clicked cell stands for), so there is no closed state
        /// in between to report, and "closed, open" would say twice over what one sentence already says.
        /// </summary>
        private void WatchDetails()
        {
            try
            {
                string system;
                Detail now = DrawnDetail(out system);
                if (now == _detail && system == _detailSystem)
                {
                    return;
                }

                Detail was = _detail;
                _detail = now;
                _detailSystem = system;
                Voice.Say(
                    now == Detail.None
                        ? ModStrings.Get(ClosedPhrase(was))
                        : ModStrings.Format(OpenedPhrase(now), system),
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: watching the panel under the table threw: " + e);
            }
        }

        /// <summary>
        /// Which panel the game has under the table, and the system it is showing.
        ///
        /// Asked of <c>Shown</c> rather than of the drawn flag the graph build uses: a panel on its way
        /// out stays Visible for the length of its fade while its replacement is already up, so a swap
        /// read off Visible would announce the panel the player just left before announcing the one they
        /// asked for. <c>Shown</c> goes false the frame Hide is called (<c>GuiPanel.OnBeginHide</c> sets
        /// Hiding), which is the frame the swap happens on.
        ///
        /// The system is the table's own selected row, which is where the game itself reads it from when
        /// it binds any of these panels (<c>ShowStarSystemPlanetCardsPanelWithActions</c> and its three
        /// siblings, :342-378) - and the hangar panel, unlike the other two, keeps no system of its own
        /// to ask.
        /// </summary>
        private Detail DrawnDetail(out string system)
        {
            system = null;
            global::EmpireScreen window = Window();
            StarSystemsManagementPanel panel =
                window == null ? null : window.StarSystemsManagementPanel;
            if (panel == null || !panel.Shown)
            {
                return Detail.None;
            }

            Detail detail = Detail.None;
            StarSystemPlanetCardsPanel cards = panel.StarSystemPlanetCardsPanel;
            StarSystemConstructiblePanel constructibles =
                Child<StarSystemConstructiblePanel>(panel.ConstructiblePanelContainer);
            StarSystemQueuePanel queue = Child<StarSystemQueuePanel>(panel.QueuePanelContainer);
            StarSystemHangarPanel hangar = Child<StarSystemHangarPanel>(panel.HangarPanelContainer);
            // Which of the three is up - the game shows exactly one at a time. The cards panel is
            // asked its MODE as well: the same panel is two different things to the player.
            if (cards != null && cards.Shown)
            {
                detail = cards.Mode == PlanetCard.DisplayMode.Actions
                    ? Detail.PlanetActions
                    : Detail.PlanetPopulation;
            }
            else if (
                (constructibles != null && constructibles.Shown)
                || (queue != null && queue.Shown)
            )
            {
                detail = Detail.Construction;
            }
            else if (hangar != null && hangar.Shown)
            {
                detail = Detail.Hangar;
            }

            if (detail != Detail.None)
            {
                system = SystemName(panel.GuiTable == null ? null : panel.GuiTable.SelectedLine);
            }

            return detail;
        }

        private static string OpenedPhrase(Detail detail)
        {
            switch (detail)
            {
                case Detail.Construction:
                    return ModStrings.EmpireConstructionPanelOpened;
                case Detail.Hangar:
                    return ModStrings.EmpireHangarPanelOpened;
                case Detail.PlanetActions:
                    return ModStrings.EmpirePlanetActionsPanelOpened;
                default:
                    return ModStrings.EmpirePopulationPanelOpened;
            }
        }

        private static string ClosedPhrase(Detail detail)
        {
            switch (detail)
            {
                case Detail.Construction:
                    return ModStrings.EmpireConstructionPanelClosed;
                case Detail.Hangar:
                    return ModStrings.EmpireHangarPanelClosed;
                case Detail.PlanetActions:
                    return ModStrings.EmpirePlanetActionsPanelClosed;
                default:
                    return ModStrings.EmpirePopulationPanelClosed;
            }
        }

        /// <summary>Whichever of the four panels the last cell click opened, in the order they are
        /// drawn. Only one kind is ever up: the game hides all of them before showing the one the
        /// clicked cell stands for, and the construction cell shows two side by side.</summary>
        private void BuildDetails(GraphBuilder builder, StarSystemsManagementPanel panel)
        {
            try
            {
                StarSystemPlanetCardsPanel cards = panel.StarSystemPlanetCardsPanel;
                // Flow control: each of these four readings descends a panel of its own.
                if (cards != null && AgeWidgets.Visible(cards.AgeTransform))
                {
                    builder.BeginStop(PlanetsStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
                    BuildCards(builder, cards);
                    builder.PopContext();
                }

                StarSystemConstructiblePanel constructibles =
                    Child<StarSystemConstructiblePanel>(panel.ConstructiblePanelContainer);
                // Flow control: a stop and a context would be opened around nothing, and the shared
                // reading walks the whole panel.
                if (constructibles != null && AgeWidgets.Visible(constructibles.AgeTransform))
                {
                    builder.BeginStop(ConstructiblesStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemConstructiblesPanel));
                    SystemPanels.Constructibles(builder, constructibles, Keys);
                    builder.PopContext();
                }

                StarSystemQueuePanel queue = Child<StarSystemQueuePanel>(panel.QueuePanelContainer);
                // Flow control: same, for the construction queue.
                if (queue != null && AgeWidgets.Visible(queue.AgeTransform))
                {
                    builder.BeginStop(QueueStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemQueuePanel));
                    SystemPanels.Queue(builder, queue, Keys);
                    builder.PopContext();
                }

                StarSystemHangarPanel hangar = Child<StarSystemHangarPanel>(panel.HangarPanelContainer);
                // Flow control: same, for the hangar.
                if (hangar != null && AgeWidgets.Visible(hangar.AgeTransform))
                {
                    builder.BeginStop(HangarStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemHangarPanel));
                    SystemPanels.Hangar(builder, hangar, Keys);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the panel under the table threw: " + e);
            }
        }

        private static T Child<T>(AgeTransform container)
            where T : UnityEngine.Component
        {
            try
            {
                return container == null ? null : container.GetComponentInChildren<T>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

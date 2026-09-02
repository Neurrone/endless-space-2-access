using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The victory tab: the sectors of the wheel and the hexes each one is filled in with.
    /// </summary>
    public sealed partial class EmpireScreen
    {
        // ---- the victory tab ----

        /// <summary>
        /// The victory wheel: one row per way of winning, in the order the wheel draws them - clockwise
        /// from the top, which is the order the panel builds its sectors in (measured).
        ///
        /// A row says what the race is and where the empire stands in it. A condition this game was set
        /// up without draws no rank at all and says so in its own sentence, which is on the tooltip
        /// where the game put it. The three figures around a sector's rim are its children.
        /// </summary>
        private void BuildVictory(GraphBuilder builder, global::EmpireScreen window)
        {
            VictoryAndPerformancePanel panel = window.VictoryAndPerformancePanel;
            AgeTransform container = panel == null ? null : panel.VictorySectorsContainer;
            if (container == null)
            {
                return;
            }

            builder.BeginStop(VictoryStop);
            string title = PanelTitle(panel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                IList<AgeTransform> sectors = container.Children;
                for (int i = 0; sectors != null && i < sectors.Count; i++)
                {
                    AddSector(builder, sectors[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the victory wheel threw: " + e);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        private void AddSector(GraphBuilder builder, AgeTransform widget, int index)
        {
            VictoryConditionSector sector =
                widget == null ? null : widget.GetComponent<VictoryConditionSector>();
            if (sector == null)
            {
                return;
            }

            VictoryConditionSector it = sector;
            AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(sector.VictoryObjectives));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.VictoryObjectives)),
                    GraphNodes.ValuePart(() => Rank(it)),
                },
                // The game writes a paragraph and the progress line into one plain tooltip, and it is
                // announced whole like every other plain one: the tooltip.s own kind decides, and this
                // page states no exception to it.
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, AgeWidgets.Transform(sector.VictoryObjectives) ?? widget);

            string key = "empire:victory/" + index;
            ControlId id = ControlId.For(widget, key);
            IList<AgeTransform> hexes = Hexes(sector);
            if (hexes == null || hexes.Count == 0)
            {
                builder.AddItem(Nodes.Drawn(id, vtable, widget));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(Nodes.Drawn(id, vtable, widget));
            if (builder.IsExpanded(id))
            {
                for (int i = 0; i < hexes.Count; i++)
                {
                    AddHex(builder, sector, hexes[i], key, i);
                }
            }

            builder.EndGroup();
        }

        /// <summary>Where the empire stands in this race, in the game's own word for the place. A
        /// condition the game was not set up with draws no rank ring at all.</summary>
        private static string Rank(VictoryConditionSector sector)
        {
            try
            {
                return sector.VictoryRankGroup == null || !sector.VictoryRankGroup.Visible
                    ? null
                    : AgeText.Label(sector.VictoryRankValue);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Hexes(VictoryConditionSector sector)
        {
            try
            {
                AgeTransform container = sector.EmpirePerformanceContainer;
                // Shape, not existence: an empty answer makes the sector a plain readout instead of an
                // expandable group, so a hidden rim must not read as a group with nothing in it.
                return container == null || !AgeWidgets.Visible(container)
                    ? null
                    : container.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One figure off a sector's rim. The hex draws a picture and a number and keeps its
        /// name in the game's own element registry, which is where the caption comes from; a tracker the
        /// corpus has no title for is left unnamed rather than read out as a key, and its own sentence -
        /// which the game does write - still says what the number counts.</summary>
        private void AddHex(
            GraphBuilder builder,
            VictoryConditionSector sector,
            AgeTransform widget,
            string keyPrefix,
            int index
        )
        {
            EmpirePerformanceHex hex =
                widget == null ? null : widget.GetComponent<EmpirePerformanceHex>();
            if (hex == null)
            {
                return;
            }

            EmpirePerformanceHex it = hex;
            string name = HexName(sector, index);
            NodeVtable vtable = GraphNodes.Readout(
                () => name,
                () => AgeText.Label(it.ValueLabel),
                null,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(
                ControlId.For(widget, keyPrefix + "/hex/" + index),
                vtable,
                widget
            ));
        }

        /// <summary>The tracker's own title. The hex holds no reference to what it is drawing once it is
        /// bound, so the sector's own definition is asked for the list it built its hexes from - the
        /// same list, in the same order, that the game handed the container.</summary>
        private static string HexName(VictoryConditionSector sector, int index)
        {
            try
            {
                VictoryConditionDefinition definition =
                    SectorDefinition == null
                        ? null
                        : SectorDefinition.GetValue(sector) as VictoryConditionDefinition;
                EmpirePerformanceTracker[] trackers =
                    definition == null ? null : definition.VisibleEmpirePerformanceTrackers;
                if (trackers == null || index >= trackers.Length || trackers[index] == null)
                {
                    return null;
                }

                Amplitude.Unity.Gui.ExtendedGuiElement element =
                    Gui.GetExtendedGuiElement(trackers[index].Name);
                // A title the corpus never wrote comes back as its own key: parked text, which is not
                // a name to speak.
                return element == null ? null : AgeText.Title(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly FieldInfo SectorDefinition = GameHandlers.Field(
            typeof(VictoryConditionSector),
            "victoryConditionDefinition"
        );
    }
}

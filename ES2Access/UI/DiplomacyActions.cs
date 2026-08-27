using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The band of things an empire can DO to somebody, wherever the game draws it: the minor-faction
    /// window's interactions, the pirate window's actions and the Academy's. Three prefab classes -
    /// <c>EmpireActionButtonMinorDiplomacy</c>, <c>PirateDiplomacyActionItem</c>,
    /// <c>AcademyDiplomacyActionItem</c> - with no common base and IDENTICAL shape: an
    /// <c>EnableGroup</c> holding a <c>Button</c>, a <c>HintButton</c> drawn only while the action is
    /// refused, a title, a description, a cost (or the game's "Free"), a political-impact block or the
    /// "no impact" stand-in for it, and a <c>Tooltip</c> the game writes the failure sentences into.
    /// So the reading is written once here and each window only says which items it drew.
    ///
    /// One node per action, and it says the whole card: the game's title, then the description, the cost
    /// and the impact as reviewable lines, and - while the action is refused - the game's own failure
    /// sentences. Enter is the button's own click, which for these is either the game's confirmation box
    /// (<c>EmpireActionGuiElement.ConfirmationMessage</c>) or an <c>OrderEntityAction</c> straight away;
    /// the mod adds no confirmation of its own and suppresses none of the game's.
    ///
    /// A refused action stays declared and REFUSING rather than disappearing - which currencies it wants
    /// and why not today is exactly what the player opened the window to find out - and the
    /// <c>HintButton</c> the game draws in its place is NOT wired: its whole job is
    /// <c>Gui.ActivateHint</c>, which closes the window and points a mouse somewhere else. An action the
    /// game has DISCARDED (the "Discard" failure flag) it hides outright, and a hidden row is not
    /// declared at all.
    /// </summary>
    public static class DiplomacyActions
    {
        /// <summary>One drawn action item, reduced to the widgets every one of the three prefabs has.
        /// <see cref="Extra"/> is a second button a row can draw beside the first - the minor window's
        /// quest button, drawn while that faction's quest is running.</summary>
        public struct Row
        {
            public AgeTransform Widget;
            public AgeControlButton Button;
            public AgeTooltip Tooltip;
            public AgePrimitiveLabel Title;
            public AgeTransform[] Lines;
            public AgeControlButton Extra;
        }

        public static void Add(List<Row> rows, EmpireActionButtonMinorDiplomacy item)
        {
            if (!Drawn(item))
            {
                return;
            }

            rows.Add(
                new Row
                {
                    Widget = item.AgeTransform,
                    Button = item.Button,
                    Tooltip = item.Tooltip,
                    Title = item.TitleLabel,
                    Lines = new AgeTransform[]
                    {
                        Of(item.DescriptionLabel),
                        item.CostGroup,
                        Of(item.PoliticalImpact),
                        item.NoPoliticalImpactGroup,
                    },
                    Extra = item.QuestButton,
                }
            );
        }

        public static void Add(List<Row> rows, PirateDiplomacyActionItem item)
        {
            if (!Drawn(item))
            {
                return;
            }

            rows.Add(
                new Row
                {
                    Widget = item.AgeTransform,
                    Button = item.Button,
                    Tooltip = item.Tooltip,
                    Title = item.TitleLabel,
                    Lines = new AgeTransform[]
                    {
                        Of(item.DescriptionLabel),
                        item.CostGroup,
                        Of(item.PoliticalImpact),
                        item.NoPoliticalImpactGroup,
                    },
                }
            );
        }

        public static void Add(List<Row> rows, AcademyDiplomacyActionItem item)
        {
            if (!Drawn(item))
            {
                return;
            }

            rows.Add(
                new Row
                {
                    Widget = item.AgeTransform,
                    Button = item.Button,
                    Tooltip = item.Tooltip,
                    Title = item.TitleLabel,
                    Lines = new AgeTransform[]
                    {
                        Of(item.DescriptionLabel),
                        item.CostGroup,
                        item.RelationsImpactGroup,
                        item.NoRelationsImpactGroup,
                        Of(item.PoliticalImpact),
                        item.NoPoliticalImpactGroup,
                    },
                }
            );
        }

        /// <summary>The collected actions, one node each, in the order the game drew them. A row that
        /// drew a SECOND button - the minor window's quest button - follows its own action as the next
        /// node rather than becoming an expandable group of one: the game draws it inside the row, and
        /// a group whose only child is one button costs the player an expansion to reach it.</summary>
        public static void Emit(GraphBuilder builder, string keyPrefix, List<Row> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                AgeControlButton button = row.Button;
                AgeTransform at = row.Widget;
                AgeTooltip tooltip = row.Tooltip;
                AgeTransform[] lines = row.Lines;
                Func<bool> offered = () =>
                    button != null && AgeWidgets.Offered(AgeWidgets.Transform(button));
                NodeVtable vtable = GraphNodes.Button(
                    Name(row.Title, tooltip),
                    () => AgeWidgets.Press(button),
                    offered,
                    tooltip,
                    null,
                    () => Details(lines)
                );
                GraphNodes.AddRefusal(vtable, tooltip, offered);
                AgeWidgets.Point(vtable, button, tooltip, at);
                builder.AddItem(Nodes.Drawn(ControlId.For(at, keyPrefix + "/action/" + i), vtable, at));

                AgeTransform extra = AgeWidgets.Transform(row.Extra);
                if (extra == null || !AgeWidgets.Visible(extra))
                {
                    continue;
                }

                AgeControlButton second = row.Extra;
                NodeVtable child = GraphNodes.Button(
                    CardActions.NameFromTooltip(extra),
                    () => AgeWidgets.Press(second),
                    () => AgeWidgets.Offered(extra),
                    AgeWidgets.Raw(extra)
                );
                AgeWidgets.Point(child, second, AgeWidgets.Raw(extra), extra);
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(extra, keyPrefix + "/action/" + i + "/extra"),
                    child,
                    extra
                ));
            }
        }

        /// <summary>
        /// The pair of stocks every window in this family draws along its bottom edge - what the player has
        /// to spend an action with.
        ///
        /// Both are drawn as a NUMBER beside an icon, with no caption and no tooltip anywhere (measured on
        /// the pirate window: neither the label nor the area around it carries one), so the number alone
        /// would be two unexplained figures. They are named by the game's own title for the property each
        /// one holds - the same names the empire banner reads them under, so "Empire Dust" means the same
        /// thing in both places.
        /// </summary>
        public static void Treasury(
            List<Cell> cells,
            AgePrimitiveLabel money,
            AgePrimitiveLabel influence,
            string keyPrefix
        )
        {
            Stock(cells, money, SimulationProperties.Empire.NetEmpireMoney, keyPrefix + "money");
            Stock(
                cells,
                influence,
                SimulationProperties.Empire.NetEmpireEmpirePoint,
                keyPrefix + "influence"
            );
        }

        private static void Stock(
            List<Cell> cells,
            AgePrimitiveLabel label,
            Amplitude.StaticString property,
            string key
        )
        {
            AgeTransform at = Of(label);
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            Amplitude.StaticString name = property;
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Clean(Gui.GetLocalizedTitle(name)),
                () => AgeWidgets.TextOf(at),
                null,
                AgeWidgets.Raw(at)
            );
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(cells, at, ControlId.For(at, key), vtable);
        }

        /// <summary>What the row is called: the title the game wrote on it, falling back to the first
        /// sentence of its own tooltip for a row whose title label the game left empty.</summary>
        private static Func<string> Name(AgePrimitiveLabel title, AgeTooltip tooltip)
        {
            AgeTransform at = Of(title);
            Func<string> fallback = CardActions.NameFromTooltip(tooltip);
            return () =>
            {
                string drawn = AgeWidgets.TextOf(at);
                return string.IsNullOrEmpty(drawn) ? fallback() : drawn;
            };
        }

        /// <summary>The rest of what the row DRAWS, one line per block the game is showing - the
        /// description, the cost or its "Free", and whichever of the impact blocks is up.</summary>
        private static IList<string> Details(AgeTransform[] lines)
        {
            List<string> said = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                AgeTransform at = lines[i];
                if (at == null || !AgeWidgets.Visible(at))
                {
                    continue;
                }

                string text = AgeWidgets.TextOf(at);
                if (!string.IsNullOrEmpty(text))
                {
                    said.Add(text);
                }
            }

            return said;
        }

        private static bool Drawn(GuiBehaviour item)
        {
            try
            {
                return item != null && AgeWidgets.Visible(item.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AgeTransform Of(AgePrimitiveLabel label)
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

        private static AgeTransform Of(GuiPanelFeature feature)
        {
            try
            {
                return feature == null ? null : feature.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

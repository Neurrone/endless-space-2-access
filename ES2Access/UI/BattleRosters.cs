using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The fleets on one side of a battle, wherever the game draws them.
    ///
    /// The same prefabs serve the setup popup, the report popup and the advanced report, in two
    /// shapes: a panel per FLEET (<c>BattleGarrisonPanel</c>, what the player brought) and, on the
    /// player's own side of a report, a panel per FLOTILLA (<c>BattleFlotillasPanel</c> holding a
    /// <c>FlotillaLine</c> each, which is how the battle actually arranged those ships). Both are read
    /// here, and which one a screen gets is the game's decision rather than the screen's.
    ///
    /// A fleet is a GROUP holding its ships: the row says what the fleet is, how many command points it
    /// is worth and whether it is running cloaked, and the ships are child nodes under it - unless it
    /// has none, in which case it is a plain row saying so (<see cref="Fleet"/>). Ships are
    /// read-only - there is nothing to do to a ship in a battle report, and the setup popup offers no
    /// per-ship choice either - so each is a line rather than a control.
    ///
    /// What a ship SAYS comes from the game three ways over. Its name is the label the row draws. Its
    /// health is the wrapper's own stat string ("120/300"), because the row draws that as a gauge with
    /// no number on it. And what BECAME of it - destroyed, kamikazed, captured by whoever, retreated
    /// because captured - is the sentence the game wrote into the row's own tooltip for exactly that
    /// purpose (<c>BattleShipItem.Refresh</c>): the mod picks no words here, it reads the game's. A ship
    /// nothing happened to has no such sentence and says nothing beyond its name and its health.
    /// </summary>
    public static class BattleRosters
    {
        /// <summary>Whether a fleet's ships are walkable straight away rather than behind an expand.
        ///
        /// Open, deliberately: the per-ship sentences ARE the report - which of your ships came back -
        /// and a player who never presses Right on a group would otherwise never hear that any of them
        /// died. The cost is Down presses past a long roster, which Alt+Down over the side's region
        /// skips in one. OWNER CALL: flip to false for a shorter read.</summary>
        private const bool ShipsOpen = true;

        /// <summary>The game's own word for a ship's health, and for the command points a fleet is
        /// worth - both drawn as pictures beside a number, or as a gauge with no number at all.</summary>
        private const string HealthTitleKey = "%ShipStatHealthTitle";
        private const string CommandPointsTitleKey = "%ShipStatCommandPointsTitle";

        /// <summary>
        /// One side's fleets, in the order the game drew them, with everything else the side's panel
        /// wrote (the citadel line, a failed retreat, a revealed camouflage) read in its place among
        /// them.
        /// </summary>
        public static void Roster(GraphBuilder builder, AgeTransform root, string prefix)
        {
            // Flow control: the whole roster below is collected by component scrapes over this root.
            if (builder == null || root == null || !AgeWidgets.Visible(root))
            {
                return;
            }

            try
            {
                List<Entry> entries = new List<Entry>();
                Collect(entries, root, prefix);
                entries.Sort(DownThePanel);
                for (int i = 0; i < entries.Count; i++)
                {
                    entries[i].Emit(builder);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a roster threw: " + e);
            }
        }

        /// <summary>
        /// One ship of a roster, as the line it is.
        ///
        /// Public because a ship is also drawn on its own - the hero's ship inside a fleet the report
        /// lists flotilla by flotilla - and reads the same wherever it is.
        ///
        /// The row has TWO hover surfaces and points at one of them: its own dossier, which names the
        /// ship and its role, and the little role badge beside the name, whose sentence says what that
        /// role is FOR ("Primary targets: Protector and then Coordinator ships") and exists nowhere
        /// else on the popup. The badge is therefore a nested entry of its own, as every second hover
        /// surface is.
        /// </summary>
        public static void Ship(GraphBuilder builder, BattleShipItem item, string key)
        {
            AgeTransform widget = item == null ? null : item.AgeTransform;
            if (widget == null)
            {
                return;
            }

            BattleShipItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                    GraphNodes.ValuePart(() => Health(it), false),
                },
                // The outcome sentence is the game.s own, kept in a field the roster never draws, so
                // the row says it as it is read - declared as a section rather than composed into the
                // readout by hand, so the same words reach the review buffer exactly once.
                Sections = GraphNodes.SpokenSections(() => OutcomeLines(it), tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);

            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            AgeTransform badge = Role(item);
            TooltipChildren.AddPlain(
                dossiers,
                AgeWidgets.Raw(badge),
                badge,
                () => RoleName(it)
            );
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(item, key), vtable, item),
                key,
                dossiers
            );
        }

        /// <summary>The role badge drawn beside a ship's name, where the game is drawing one - it hides
        /// the badge outright for a ship whose data names no role.</summary>
        private static AgeTransform Role(BattleShipItem item)
        {
            try
            {
                return item.RoleIcon == null ? null : item.RoleIcon.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the role badge is CALLED: the game's own title for the role its picture stands
        /// for, which is the same element the badge takes its sentence from
        /// (<c>BattleShipItem.Refresh</c>). Null where the game will not say, and the ordinary naming
        /// ladder answers instead.</summary>
        private static string RoleName(BattleShipItem item)
        {
            try
            {
                GuiBattleShip ship = item.GuiBattleShip;
                Amplitude.StaticString role = ship == null ? Amplitude.StaticString.Empty : ship.Role;
                Amplitude.Unity.Gui.GuiElement element =
                    Amplitude.StaticString.IsNullOrEmpty(role) ? null : Gui.GetGuiElement(role);
                return element == null ? null : AgeText.Clean(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How hurt a ship is, in the game's own stat string with the game's own word in front
        /// of it. The row draws this as a bar and writes no number, so there is nothing to read off the
        /// screen; the wrapper the row is bound to is where the game keeps it.</summary>
        private static string Health(BattleShipItem item)
        {
            try
            {
                GuiBattleShip ship = item.GuiBattleShip;
                if (ship == null)
                {
                    return null;
                }

                return new MessageBuilder()
                    .ListItem(AgeText.Clean(HealthTitleKey))
                    .ListItem(AgeText.Clean(ship.ShipStatHealthProperty))
                    .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What became of the ship, as the game's own sentence.
        ///
        /// The row uses its tooltip for two different things and says which by the CLASS it asks for:
        /// the plain ship dossier carries the ship's name in Content and nothing worth speaking, while a
        /// ship something happened to gets the "with simple" variant, whose Content is the whole
        /// sentence the game wrote about it. So a status is present exactly when the game chose to write
        /// one, and the mod invents nothing for the rest.
        /// </summary>
        private static IList<string> OutcomeLines(BattleShipItem item)
        {
            string said = Outcome(item);
            List<string> lines = new List<string>(1);
            if (!string.IsNullOrEmpty(said))
            {
                lines.Add(said);
            }

            return lines;
        }

        private static string Outcome(BattleShipItem item)
        {
            try
            {
                AgeTooltip tooltip = item.Tooltip;
                return tooltip == null || tooltip.Class != ShipWithStatusTooltipClass
                    ? null
                    : AgeText.Clean(tooltip.Content);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string ShipWithStatusTooltipClass = "ShipWithSimple";

        private static void Collect(List<Entry> entries, AgeTransform root, string prefix)
        {
            // The fleet the player brought, and its ships.
            BattleGarrisonPanel[] garrisons = root.GetComponentsInChildren<BattleGarrisonPanel>(true);
            for (int i = 0; i < garrisons.Length; i++)
            {
                BattleGarrisonPanel panel = garrisons[i];
                // The collected entries are SORTED by rectangle and read in that order, so a panel the
                // report is not drawing must never enter the list - its stale rectangle would reorder
                // the ones that are.
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    continue;
                }

                BattleGarrisonPanel it = panel;
                string key = prefix + "/garrison/" + i;
                entries.Add(
                    new Entry
                    {
                        Widget = panel.AgeTransform,
                        Emit = builder =>
                            Fleet(
                                builder,
                                it,
                                ControlId.For(it, key),
                                it,
                                () => FleetName(it),
                                it.BattleShipItemsTable,
                                key
                            ),
                    }
                );
            }

            // The same ships as the battle arranged them: a header for the fleet, a group per flotilla.
            BattleFlotillasPanel[] flotillas = root.GetComponentsInChildren<BattleFlotillasPanel>(true);
            for (int i = 0; i < flotillas.Length; i++)
            {
                BattleFlotillasPanel panel = flotillas[i];
                // Same ordering: an undrawn panel would take a place in the sorted reading.
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    continue;
                }

                Flotillas(entries, panel, prefix + "/flotillas/" + i);
            }

            // Everything else the side's panel wrote: the citadel that is firing for it, a retreat that
            // failed, a camouflage the battle blew. Each is a label the game fills in and hides again,
            // and each is one line.
            Note(entries, Citadel(root), prefix + "/citadel");
            Note(entries, RetreatFailed(root), prefix + "/retreat-failed");
            Note(entries, CamouflageRevealed(root), prefix + "/camouflage");
        }

        private static void Flotillas(
            List<Entry> entries,
            BattleFlotillasPanel panel,
            string prefix
        )
        {
            AgeTransform header = panel.GarrisonTitleGroup;
            // Different widget as well as ordering: the entry stands on the PANEL and this asks about
            // the header group inside it.
            if (header != null && AgeWidgets.Visible(header))
            {
                BattleFlotillasPanel it = panel;
                entries.Add(
                    new Entry
                    {
                        Widget = header,
                        Emit = builder =>
                            builder.AddItem(Nodes.Drawn(
                                ControlId.For(it, prefix + "/name"),
                                Explained(
                                    Line(() => FleetName(it)),
                                    Header(it.GarrisonTitleGroup, it.CommandPointsGroup)
                                ),
                                it
                            )),
                    }
                );
            }

            AgeTransform table = panel.FlotillaLinesTable;
            if (table == null)
            {
                return;
            }

            FlotillaLine[] lines = table.GetComponentsInChildren<FlotillaLine>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                FlotillaLine line = lines[i];
                // Same ordering as the panels above.
                if (line == null || !AgeWidgets.Visible(line.AgeTransform))
                {
                    continue;
                }

                FlotillaLine it = line;
                string key = prefix + "/line/" + i;
                entries.Add(
                    new Entry
                    {
                        Widget = line.AgeTransform,
                        Emit = builder =>
                            Fleet(
                                builder,
                                null,
                                ControlId.For(it, key),
                                it,
                                () => FlotillaName(it),
                                it.BattleShipItemsTable,
                                key
                            ),
                    }
                );
            }
        }

        /// <summary>
        /// One fleet or flotilla: the row that names it, and the ships under it.
        ///
        /// A fleet with NO SHIPS is a plain row and not a group - a flotilla the battle left empty
        /// says so in the game's own word for it ("Flotilla 1, Empty") and there is nothing inside it
        /// to go into (owner-reported 2026-08-29; before this it was a group that expanded to
        /// nothing). The count is taken before the row is declared, which is why the ships are
        /// collected first.
        /// </summary>
        private static void Fleet(
            GraphBuilder builder,
            BattleGarrisonPanel panel,
            ControlId id,
            object drawnBy,
            Func<string> name,
            AgeTransform ships,
            string prefix
        )
        {
            AgeTooltip tooltip = panel == null
                ? null
                : Header(panel.GarrisonTitleGroup, panel.CommandPointsGroup);
            BattleShipItem[] items = ships == null
                ? new BattleShipItem[0]
                : ships.GetComponentsInChildren<BattleShipItem>(true);
            // No role word on the empty one either: with nothing inside it there is no group here,
            // and "Flotilla 1, Empty" is the whole of what the line says.
            NodeDeclaration row = Nodes.Drawn(
                id,
                items.Length == 0
                    ? Explained(Line(name), tooltip)
                    : GraphNodes.Group(name, null, tooltip),
                drawnBy
            );
            if (items.Length == 0)
            {
                builder.AddItem(row);
                return;
            }

            builder.BeginGroup(row, null, ShipsOpen);
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    Ship(builder, items[i], prefix + "/ship/" + i);
                }
            }
            finally
            {
                // The parent stack has to come back balanced whatever a ship's own reading did, or
                // everything the popup declares after this fleet lands inside it.
                builder.EndGroup();
            }
        }

        /// <summary>What a fleet is called, and what it is worth: the name the panel drew (with the
        /// game's own "(merged)" where several fleets are fighting as one), the command points the whole
        /// of it costs, and the cloaked marker where the panel is showing one.</summary>
        private static string FleetName(BattleGarrisonPanel panel)
        {
            try
            {
                return Named(
                    panel.GarrisonTitleLabel,
                    panel.CommandPointsGroup,
                    panel.CommandPointsLabel,
                    panel.InvisibilityGroup
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FleetName(BattleFlotillasPanel panel)
        {
            try
            {
                return Named(
                    panel.GarrisonTitleLabel,
                    panel.CommandPointsGroup,
                    panel.CommandPointsLabel,
                    panel.InvisibilityGroup
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Named(
            AgePrimitiveLabel title,
            AgeTransform commandPointsGroup,
            AgePrimitiveLabel commandPoints,
            AgeTransform invisibility
        )
        {
            MessageBuilder name = new MessageBuilder().ListItem(AgeText.Label(title));
            // Content: whether the command-points figure joins the name.
            if (commandPoints != null && AgeWidgets.Visible(commandPointsGroup))
            {
                name.ListItem(AgeText.Clean(CommandPointsTitleKey))
                    .ListItem(AgeText.Label(commandPoints));
            }

            // Content: whether the invisibility word joins it.
            if (AgeWidgets.Visible(invisibility))
            {
                name.ListItem(CardActions.FirstLine(AgeWidgets.Raw(invisibility)));
            }

            return name.Build();
        }

        /// <summary>Which flotilla this is, in the game's own numbering - the line draws the number
        /// alone, and a row that said "2" would be saying nothing.</summary>
        private static string FlotillaName(FlotillaLine line)
        {
            try
            {
                string index = AgeText.Label(line.FlotillaIndexLabel);
                string named = string.IsNullOrEmpty(index)
                    ? null
                    : AgeText.Clean(Gui.Localize(FlotillaNameKey, index));
                return new MessageBuilder()
                    .ListItem(named ?? index)
                    .ListItem(AgeWidgets.DrawnLabel(line.EmptyLabel))
                    .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string FlotillaNameKey = "%FlotillaNameTitle";

        /// <summary>A label's own rectangle - what says whether the player can see it, and what its
        /// place among the other rows is worked out from.</summary>
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

        private static void Note(List<Entry> entries, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = Widget(label);
            if (
                label == null
                // Same ordering: an undrawn note would take a place in the sorted reading.
                || !AgeWidgets.Visible(widget)
                || string.IsNullOrEmpty(AgeText.Label(label))
            )
            {
                return;
            }

            AgePrimitiveLabel it = label;
            entries.Add(
                new Entry
                {
                    Widget = widget,
                    Emit = builder =>
                        builder.AddItem(Nodes.Drawn(
                            ControlId.For(it, key),
                            Line(() => AgeText.Label(it)),
                            it
                        )),
                }
            );
        }

        /// <summary>A line the player reads rather than works - no role word, because there is no
        /// control here.</summary>
        private static NodeVtable Line(Func<string> text)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(text) },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
        }

        /// <summary>The same line with the tooltip that explains it, where the panel drew one - the
        /// door does the aiming, and a line with nothing to explain it keeps the released pointer it
        /// had.</summary>
        private static NodeVtable Explained(NodeVtable vtable, AgeTooltip tooltip)
        {
            if (tooltip != null)
            {
                vtable.Sections = GraphNodes.SectionsFor(vtable, tooltip);
            }

            return vtable;
        }

        /// <summary>
        /// What explains a fleet's header: the tooltip the panel hung on the title where it has one,
        /// else the one on the command-points badge beside it.
        ///
        /// The badge's sentence ("The total Command Points in this fleet") is the only thing on the
        /// popup that says what the figure the header row reads out actually IS, and no prefab here
        /// puts a tooltip on the title group at all - so the header row would otherwise say a number
        /// with the game's own caption for it and nothing about what it counts.
        /// </summary>
        private static AgeTooltip Header(AgeTransform title, AgeTransform commandPoints)
        {
            // Different widget: the row stands on the panel and this asks about the badge inside it,
            // the same badge whose figure the row only names while the game is drawing it
            // (see Named) - a header explained by a command-points sentence it is not showing would
            // be describing a figure it did not say.
            return AgeWidgets.Raw(title)
                ?? (AgeWidgets.Visible(commandPoints) ? AgeWidgets.Raw(commandPoints) : null);
        }

        private static AgePrimitiveLabel Citadel(AgeTransform root)
        {
            BattleGroupSetupPanel panel = root.GetComponent<BattleGroupSetupPanel>();
            return panel == null ? null : panel.ProtectedByCitadelLabel;
        }

        private static AgePrimitiveLabel RetreatFailed(AgeTransform root)
        {
            BattleGroupReportPanel panel = root.GetComponent<BattleGroupReportPanel>();
            return panel == null ? null : panel.RetreatFailedLabel;
        }

        private static AgePrimitiveLabel CamouflageRevealed(AgeTransform root)
        {
            EnemyBattleGroupReportPanel panel = root.GetComponent<EnemyBattleGroupReportPanel>();
            return panel == null ? null : panel.CamouflageRevealedLabel;
        }

        /// <summary>One thing the side's panel drew, held with the widget it was read off so the whole
        /// side can be walked in the order it appears rather than in the order the components were
        /// found.</summary>
        private sealed class Entry
        {
            public AgeTransform Widget;
            public Action<GraphBuilder> Emit;
        }

        private static readonly Comparison<Entry> DownThePanel = delegate(Entry a, Entry b)
        {
            return AgeLayout.TopThenLeft(a.Widget, b.Widget);
        };
    }
}

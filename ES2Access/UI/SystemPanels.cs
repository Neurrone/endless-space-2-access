using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The three panels the game slides out under a system: what it can build, what it is building, and
    /// what is parked in its hangar.
    ///
    /// They are PREFABS, not places. The star system page instantiates one of each along its bottom
    /// edge; the Empire summary's systems tab instantiates a SECOND set of the same three into its own
    /// containers and shows whichever one the clicked cell stands for. Same widgets, same clicks, same
    /// orders - so the reading lives here, and each page passes in the instance it is drawing and the
    /// prefix its ids are keyed under.
    ///
    /// What is NOT here is layout: which stops these become, in what order, and what they are called is
    /// each page's own answer - the star system page draws all three at once along the bottom while the
    /// Empire screen draws one at a time in a band under its table.
    ///
    /// The one thing the two instances genuinely differ in is the flying icon a queued construction
    /// plays: the star system page registers itself as the panel's <c>ConstructionObserver</c> and the
    /// Empire screen's copy leaves it null (measured), so the acknowledgement is asked of the panel's
    /// own observer rather than of a window.
    /// </summary>
    public static class SystemPanels
    {
        /// <summary>Reused across builds rather than allocated per frame: these run every tick, and one
        /// page builds at a time.</summary>
        private static readonly List<Cell> Scratch = new List<Cell>();

        /// <summary>
        /// What this system can be told to build: the filters that decide which of them are shown, then
        /// the items themselves in the order the grid lays them out.
        ///
        /// Enter puts one at the end of the queue and Alt and Enter at the front, which are the game's
        /// own click and its own Alt-click. A confirmation the game wants for a particular thing -
        /// scrapping the colony, most of them - is asked exactly as the game asks it, through the
        /// message box that is already a screen of ours.
        /// </summary>
        public static void Constructibles(
            GraphBuilder builder,
            StarSystemConstructiblePanel panel,
            string keyPrefix
        )
        {
            try
            {
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                Scratch.Clear();
                AgeTransform filters = panel.ConstructibleFiltersTable;
                if (filters != null && AgeWidgets.Visible(filters))
                {
                    ConstructibleFilter[] all = filters.GetComponentsInChildren<ConstructibleFilter>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        AddFilter(Scratch, all[i], keyPrefix);
                    }
                }

                Cells.Emit(builder, Scratch);

                Scratch.Clear();
                AgeTransform table = panel.ConstructibleTable;
                if (table != null)
                {
                    StarSystemConstructibleItem[] items =
                        table.GetComponentsInChildren<StarSystemConstructibleItem>(true);
                    for (int i = 0; i < items.Length; i++)
                    {
                        AddConstructible(Scratch, items[i], panel, keyPrefix);
                    }
                }

                Cells.Emit(builder, Scratch);
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading the constructibles threw: " + e);
            }
        }

        private static void AddFilter(List<Cell> cells, ConstructibleFilter filter, string keyPrefix)
        {
            if (filter == null || !AgeWidgets.Visible(filter.AgeTransform))
            {
                return;
            }

            AgeControlToggle toggle = filter.Toggle;
            if (toggle == null)
            {
                return;
            }

            ConstructibleFilter it = filter;
            AgeTooltip tooltip = filter.Tooltip;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => CardActions.FirstLine(tooltip),
                () => it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(it.AgeTransform),
                tooltip,
                TooltipMode.None
            );
            AgeWidgets.PointAt(vtable, filter.AgeTransform);
            Cells.Add(
                cells,
                filter.AgeTransform,
                ControlId.Referenced(filter, keyPrefix + "filter/" + filter.name),
                vtable
            );
        }

        private static void AddConstructible(
            List<Cell> cells,
            StarSystemConstructibleItem item,
            StarSystemConstructiblePanel panel,
            string keyPrefix
        )
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
            StarSystemConstructiblePanel owner = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(item.AgeTransform);
            Func<IList<string>> drawn = GraphNodes.TooltipDetails(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ConstructibleName(it)),
                    GraphNodes.ValuePart(() => ConstructibleCost(it, owner)),
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(it.AgeTransform)),
                },
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip),
                    NodeSection.Buffer(() => ConstructibleFailures(it, drawn))
                ),
                OnActivate = () => QueueConstruction(it, owner, false),
                OnAlternate = () => QueueConstruction(it, owner, true),
            };
            // The tile's tooltip is the renderer-assembled kind, so it is only indicated - and a tile the
            // game is refusing would then say "unavailable" and nothing else. The reason is read off the
            // wrapper the tooltip carries, as its failure panel does.
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Operable(it.AgeTransform));
            AgeWidgets.PointAt(vtable, item.AgeTransform);
            Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.Referenced(item, keyPrefix + "constructible/" + constructible.Name),
                vtable
            );
        }

        /// <summary>The item's full name. The grid clips its caption to fit the tile - "Cerebral ." -
        /// so the name is taken from what the tile is FOR rather than from what the tile says.
        /// </summary>
        private static string ConstructibleName(StarSystemConstructibleItem item)
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

        private static string ConstructibleCost(
            StarSystemConstructibleItem item,
            StarSystemConstructiblePanel panel
        )
        {
            try
            {
                float cost = item.GuiConstructible.GetIndustryCost(panel.ColonizedStarSystem);
                return cost <= 0f ? null : ModStrings.Format(ModStrings.SystemIndustryCost, Amount(cost));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The game's own reasons for refusing this tile, which it collects on the item as it works out
        /// whether to offer it. Read from the item rather than from the tooltip so the reasons are in
        /// the buffer the moment focus lands, before the tooltip window has drawn its failure panel.
        ///
        /// <paramref name="drawn"/> is that tooltip's own lines, and a reason already among them is
        /// dropped: once the panel is up it says the same sentence, and the tile's two sections would
        /// otherwise put it into the buffer twice.
        /// </summary>
        private static IList<string> ConstructibleFailures(
            StarSystemConstructibleItem item,
            Func<IList<string>> drawn
        )
        {
            List<string> lines = new List<string>();
            try
            {
                AddFailures(lines, item.FailureInfosProvider);
                IList<string> already = drawn == null ? null : drawn();
                for (int i = lines.Count - 1; already != null && i >= 0; i--)
                {
                    if (already.Contains(lines[i]))
                    {
                        lines.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading a constructible's refusals threw: " + e);
            }

            return lines;
        }

        private static void AddFailures(List<string> lines, IFailureInfosProvider provider)
        {
            if (provider == null || provider.FailureInfos == null)
            {
                return;
            }

            for (int i = 0; i < provider.FailureInfos.Count; i++)
            {
                string text = AgeText.Clean(
                    Gui.FormatFailure(string.Empty, provider.FailureInfos[i].Flag.ToString())
                );
                if (!string.IsNullOrEmpty(text) && !lines.Contains(text))
                {
                    lines.Add(text);
                }
            }
        }

        /// <summary>
        /// Put a thing in the queue, the way the panel's own click does - including the confirmation
        /// the game insists on for the few constructions it will not let you queue by accident, asked
        /// with the game's own words through the game's own message box.
        ///
        /// <paramref name="atHead"/> is the game's Alt-click: the same order, followed by a move to the
        /// front once the game has accepted it and there is something to move.
        /// </summary>
        private static void QueueConstruction(
            StarSystemConstructibleItem item,
            StarSystemConstructiblePanel panel,
            bool atHead
        )
        {
            try
            {
                if (!AgeWidgets.Operable(item.AgeTransform))
                {
                    return;
                }

                ColonizedStarSystem system = panel.ColonizedStarSystem;
                IConstructible constructible = item.GuiConstructible.Constructible;
                if (system == null || constructible == null)
                {
                    return;
                }

                if (constructible.NeedsConfirmation)
                {
                    StarSystemConstructibleItem confirmed = item;
                    StarSystemConstructiblePanel owner = panel;
                    Gui.GuiService.ShowMessage(
                        GuiConstructibleElement.GetConfirmationMessage(
                            constructible,
                            Gui.GetActivePlayerController().Empire as Empire,
                            system.GUID
                        ),
                        MessageBoxType.IMPORTANT,
                        (sender, result) =>
                        {
                            if (result.Result == MessageBoxResult.Ok)
                            {
                                Post(confirmed, owner, atHead);
                            }
                        }
                    );
                    return;
                }

                Post(item, panel, atHead);
            }
            catch (Exception e)
            {
                Log.Warn("system panels: queueing a construction threw: " + e);
            }
        }

        private static void Post(
            StarSystemConstructibleItem item,
            StarSystemConstructiblePanel panel,
            bool atHead
        )
        {
            try
            {
                ColonizedStarSystem system = panel.ColonizedStarSystem;
                PlayerController player = Gui.GetActivePlayerController();
                OrderQueueConstruction order = new OrderQueueConstruction(
                    player.Empire.Index,
                    system.GUID,
                    item.GuiConstructible.Constructible
                );
                if (atHead)
                {
                    Ticket ignored;
                    player.PostOrder(
                        order,
                        out ignored,
                        (sender, args) => MoveToHead(args, system)
                    );
                }
                else
                {
                    player.PostOrder(order);
                }

                // The flying icon the panel draws when a click queues something: the page looks the
                // same to someone watching whether the queue was filled by hand or by keyboard. The
                // Empire screen's copy of the panel has nobody watching for it, and then there is
                // nothing to play.
                if (panel.ConstructionObserver != null)
                {
                    panel.ConstructionObserver.AcknowledgeConstruction(
                        item.AgeTransform,
                        item.Icon.Image
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system panels: posting a construction order threw: " + e);
            }
        }

        private static void MoveToHead(TicketRaisedEventArgs args, ColonizedStarSystem system)
        {
            try
            {
                if (args.Result != PostOrderResponse.Processed)
                {
                    return;
                }

                OrderQueueConstruction queued = args.Order as OrderQueueConstruction;
                PlayerController player = Gui.GetActivePlayerController();
                player.PostOrder(
                    new OrderMoveConstruction(
                        player.Empire.Index,
                        system.GUID,
                        queued.ConstructionGameEntityGUID,
                        0
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system panels: moving a construction to the head threw: " + e);
            }
        }

        /// <summary>
        /// What the system is building, in order. Enter is the line's own click, which is the game's
        /// cancel: instant while nothing has been invested in the thing, and its OWN confirmation box
        /// once something has (<c>StarSystemQueuePanel.OnCancelConstruction</c> :425-442).
        ///
        /// The queue is REORDERED by carrying: Space picks a line up, Enter on another line drops it
        /// there, and it lands at that line's own position - which is what the game's drag does
        /// (<c>StarSystemQueuePanel.OnDragCompleted</c> :302-320 posts <c>OrderMoveConstruction</c>
        /// with the sibling index the line was dragged into, and <c>ConstructionQueue.Move</c>
        /// :156-176 removes and re-inserts at that index).
        /// </summary>
        public static void Queue(GraphBuilder builder, StarSystemQueuePanel panel, string keyPrefix)
        {
            try
            {
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                AgeTransform table = panel.ConstructionLinesTable;
                if (table == null)
                {
                    return;
                }

                ConstructionLine[] lines = table.GetComponentsInChildren<ConstructionLine>(true);
                int drawn = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (Queued(lines[i]))
                    {
                        drawn++;
                    }
                }

                // A line can only be carried where there is another line to drop it on: one thing in
                // the queue is not a thing that can be reordered, so it is not a pick-up either.
                for (int i = 0; i < lines.Length; i++)
                {
                    AddQueueLine(builder, lines[i], panel, keyPrefix, drawn > 1);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading the construction queue threw: " + e);
            }
        }

        private static bool Queued(ConstructionLine line)
        {
            return line != null
                && AgeWidgets.Visible(line.AgeTransform)
                && line.Construction != null;
        }

        private static void AddQueueLine(
            GraphBuilder builder,
            ConstructionLine line,
            StarSystemQueuePanel panel,
            string keyPrefix,
            bool canCarry
        )
        {
            if (!Queued(line))
            {
                return;
            }

            ConstructionLine it = line;
            StarSystemQueuePanel owner = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(line.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                    GraphNodes.ValuePart(() => QueueLineState(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                // The line's own click - the game's cancel, with the game's own confirmation where it
                // wants one. Pressed through MainButton rather than through the panel's handler, so
                // the god-mode branch and the mid-drag guard the game puts in front of it stay in.
                OnActivate = () => AgeWidgets.Press(it.MainButton),
                DropKind = QueueKind,
                OnDrop = held => DropInQueue(it, owner, held),
            };
            if (canCarry)
            {
                vtable.OnPickUp = () => Pick(it);
            }

            if (ModEntry.Carry != null)
            {
                vtable.Announcements.Add(ModEntry.Carry.DropTargetPart(QueueKind));
            }

            AgeWidgets.PointAt(vtable, line.AgeTransform);
            string key = keyPrefix + "queue/" + line.Construction.GUID;
            ControlId id = ControlId.Referenced(line.Construction, key);
            List<CardActions.CardAction> buyouts = BuyoutButtons(line);
            if (buyouts.Count == 0)
            {
                builder.AddItem(id, vtable);
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(id, vtable);
            if (builder.IsExpanded(id))
            {
                CardActions.Emit(builder, key, buyouts);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// The buy-out buttons the line draws along its right-hand end, one per currency the game is
        /// willing to consider. A refused one is left DRAWN and switched off with the reason in its own
        /// tooltip (<c>ConstructionLine.RefreshBuyout</c> :272-343), so it is declared and refusing
        /// rather than dropped - the player hears which currencies exist here and why today's answer is
        /// no. One the game has hidden outright (missing technology, wrong affinity, another empire's
        /// system) is not offered at all.
        /// </summary>
        private static List<CardActions.CardAction> BuyoutButtons(ConstructionLine line)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(2);
            try
            {
                BuyoutButton[] buyouts = line.BuyoutButtons;
                for (int i = 0; buyouts != null && i < buyouts.Length; i++)
                {
                    BuyoutButton buyout = buyouts[i];
                    if (buyout == null || !AgeWidgets.Visible(buyout.AgeTransform))
                    {
                        continue;
                    }

                    BuyoutButton it = buyout;
                    CardActions.AddRefusable(
                        found,
                        buyout.AgeTransform,
                        () =>
                            ModStrings.Format(
                                ModStrings.SystemBuyOut,
                                AgeText.Clean(Gui.GetLocalizedTitle("Empire" + it.Resource))
                            )
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading a queue line's buy-out buttons threw: " + e);
            }

            return found;
        }

        /// <summary>Which cargo a construction queue line takes - its own queue's, so nothing else the
        /// page can carry lands in it.</summary>
        private const string QueueKind = "construction-queue";

        /// <summary>The construction this line stands for, picked up.</summary>
        private static CarryItem Pick(ConstructionLine line)
        {
            try
            {
                return new CarryItem(line.Construction, AgeText.Label(line.Title), QueueKind);
            }
            catch (Exception e)
            {
                Log.Warn("system panels: picking a queue line up threw: " + e);
                return null;
            }
        }

        /// <summary>Put a carried construction at this line's place in the queue - the index the game's
        /// own drag posts when a line is dropped on this slot.</summary>
        private static DropResult DropInQueue(
            ConstructionLine target,
            StarSystemQueuePanel panel,
            CarryItem held
        )
        {
            try
            {
                Construction construction = held.Cargo as Construction;
                ColonizedStarSystem system = panel.ColonizedStarSystem;
                ConstructionQueue queue = QueueOf(system);
                if (construction == null || queue == null)
                {
                    return DropResult.Refused();
                }

                int index = queue.IndexOf(target.Construction);
                if (index < 0 || !queue.Contains(construction))
                {
                    return DropResult.Refused();
                }

                if (queue.IndexOf(construction) == index)
                {
                    // Dropped back where it started - the same case the game's own
                    // <c>OnDragCompleted</c> answers by cancelling the drag without posting anything.
                    return DropResult.Done(ModStrings.Get(ModStrings.CarryCancelled));
                }

                PlayerController player = Gui.GetActivePlayerController();
                player.PostOrder(
                    new OrderMoveConstruction(
                        player.Empire.Index,
                        system.GUID,
                        construction.GUID,
                        index
                    )
                );
                return DropResult.Done(
                    ModStrings.Format(ModStrings.CarryMovedToPosition, held.Name, index + 1)
                );
            }
            catch (Exception e)
            {
                Log.Warn("system panels: moving a construction in the queue threw: " + e);
                return DropResult.Refused();
            }
        }

        private static ConstructionQueue QueueOf(ColonizedStarSystem system)
        {
            DepartmentOfIndustry industry =
                system == null ? null : system.Empire.GetAgency<DepartmentOfIndustry>();
            return industry == null ? null : industry.GetConstructionQueue(system);
        }

        /// <summary>Where the line is in the queue, how far along it is, and how long is left - the
        /// three things the line draws beside its name.</summary>
        private static string QueueLineState(ConstructionLine line)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                message.ListItem(
                    ModStrings.Format(ModStrings.SystemQueuePosition, AgeText.Label(line.Rank))
                );
                if (line.Progress != null && line.Progress.Visible)
                {
                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.SystemProgress,
                            Mathf.RoundToInt(line.Progress.PercentRight)
                        )
                    );
                }

                if (line.RemainingTurnLabel != null && line.RemainingTurnLabel.Visible)
                {
                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.GalaxyTurnsRemaining,
                            AgeText.Label(line.RemainingTurnLabel)
                        )
                    );
                }
            }
            catch (Exception) { }

            return message.Build();
        }

        /// <summary>
        /// The ships parked in the system: the row of things that can be done to a selection, then the
        /// ships themselves. Enter picks ships OUT rather than doing anything to them, because that is
        /// the game's own model here - you choose ships and then press a button. Nothing is carried
        /// here: neither page that draws this panel draws a fleet line beside it, so a ship picked up
        /// would have nowhere to be put down.
        ///
        /// An EMPTY hangar says so, in the mod's own words. The game draws the toolbar over an empty area
        /// with no placeholder of any kind, so all a player heard was a row of buttons refusing - and
        /// "nothing here" and "here are five things you cannot do" are not the same news.
        /// </summary>
        public static void Hangar(GraphBuilder builder, ShipsManagementPanel panel, string keyPrefix)
        {
            try
            {
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                string keys = keyPrefix + "hangar";
                Scratch.Clear();
                ShipRows.Toolbar(Scratch, panel, keys);
                Cells.Emit(builder, Scratch);

                Scratch.Clear();
                ShipRows.Ships(Scratch, panel, keys, false);
                if (Scratch.Count == 0)
                {
                    builder.AddItem(
                        ControlId.Structural(keys + "/empty"),
                        GraphNodes.Readout(
                            () => ModStrings.Get(ModStrings.SystemHangarEmpty),
                            null,
                            null,
                            null
                        )
                    );
                    return;
                }

                Cells.Emit(builder, Scratch);
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading the hangar threw: " + e);
            }
        }

        private static string Amount(float value)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, false, 0);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

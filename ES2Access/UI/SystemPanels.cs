using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI.Input;
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
        /// <summary>
        /// Reused across builds rather than allocated per frame: these run every tick, and one page
        /// builds at a time. Two lists because both halves of a panel are counted before either is
        /// declared - a region is only worth naming where there is a second one to jump to.
        /// </summary>
        private static readonly List<Cell> Bar = new List<Cell>();
        private static readonly List<Cell> Grid = new List<Cell>();

        /// <summary>Empty the scratch - mod teardown. Whatever the last build left in them is a
        /// list of widgets belonging to a page nobody can reach any more.</summary>
        public static void Forget()
        {
            Bar.Clear();
            Grid.Clear();
        }

        /// <summary>
        /// What this system can be told to build: the filters that decide which of them are shown, then
        /// the items themselves in the order the grid lays them out.
        ///
        /// Two regions, because the game draws two halves and neither is captioned: the switches that
        /// decide what is listed, and the list. The words over them are the mod's own, and are the ones
        /// the ship designer's module band already uses for the same two halves - a player who has met
        /// one meets the same pair here.
        ///
        /// The switches stay ONE row: they are a select-one group the panel re-derives from the filter
        /// in force, and the row they are drawn in is the row the player walks (owner ruling). The items
        /// under them are one per row - a grid of tiles whose wrap points are the table's, not the
        /// game's.
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
                // Flow control: the band and the grid below are each walked item by item.
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                Bar.Clear();
                AgeTransform filters = panel.ConstructibleFiltersTable;
                // Flow control: the filter strip below is walked toggle by toggle.
                if (filters != null && AgeWidgets.Visible(filters))
                {
                    ConstructibleFilter[] all = filters.GetComponentsInChildren<ConstructibleFilter>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        AddFilter(Bar, all[i], keyPrefix);
                    }
                }

                Grid.Clear();
                AgeTransform table = panel.ConstructibleTable;
                if (table != null)
                {
                    StarSystemConstructibleItem[] items =
                        table.GetComponentsInChildren<StarSystemConstructibleItem>(true);
                    for (int i = 0; i < items.Length; i++)
                    {
                        AddConstructible(Grid, items[i], panel, keyPrefix);
                    }
                }

                bool regions = Bar.Count > 0 && Grid.Count > 0;
                Cells.EmitRegion(
                    builder,
                    keyPrefix + "constructibles/filters",
                    ModStrings.ShipDesignFilters,
                    regions,
                    Bar,
                    Cells.AsDrawnRows
                );
                Cells.EmitRegion(
                    builder,
                    keyPrefix + "constructibles/list",
                    ModStrings.ShipDesignAvailable,
                    regions,
                    Grid,
                    Cells.OnePerRow
                );
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading the constructibles threw: " + e);
            }
        }

        /// <summary>
        /// One of the four things the grid can be narrowed to - everything, improvements, the ones that
        /// can be built over and over, ships.
        ///
        /// They are RADIO buttons and not tick boxes, because that is what the game made them: its own
        /// handler only ever SETS which filter is in force
        /// (<c>StarSystemConstructiblePanel.OnToggleConstructibleFilter</c> :491-495) and its next
        /// refresh writes every toggle's state back from that one name (<c>BindConstructibleFilter</c>
        /// :417-425). Declared as tick boxes, unticking one flipped it locally and the refresh snapped it
        /// straight back, which the live value watch then read out as a burst of checked/unchecked.
        /// </summary>
        private static void AddFilter(List<Cell> cells, ConstructibleFilter filter, string keyPrefix)
        {
            if (filter == null)
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
            NodeVtable vtable = GraphNodes.Radio(
                () => CardActions.FirstLine(tooltip),
                () => it.Toggle.State,
                () => AgeWidgets.Select(it.Toggle),
                () => AgeWidgets.Operable(it.AgeTransform),
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, filter.AgeTransform);
            Cells.Add(
                cells,
                filter.AgeTransform,
                ControlId.For(filter, keyPrefix + "filter/" + filter.name),
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
            if (item == null)
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
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(it.AgeTransform)),
                },
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip),
                    NodeSection.Buffer(() => ConstructibleFailures(it, drawn))
                ),
                OnActivate = () => QueueConstruction(it, owner, false),
                OnAlternate = () => QueueConstruction(it, owner, true),
            };
            // The tile draws nothing about the second gesture and the queue it changes is a panel
            // away, so the buffer says it.
            NodeHints.Add(vtable, ModStrings.HintQueueFirst, UiActions.Alternate);
            // The tile's tooltip is the renderer-assembled kind, so it is only indicated - and a tile the
            // game is refusing would then say "unavailable" and nothing else. The reason is read off the
            // wrapper the tooltip carries, as its failure panel does.
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Operable(it.AgeTransform));
            AgeWidgets.PointAt(vtable, item.AgeTransform);
            Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.For(item, keyPrefix + "constructible/" + constructible.Name),
                vtable
            );

            // The tile draws badges beside its name, each with its own explanation behind it: what the
            // ship design this tile builds is FOR (<c>StarSystemConstructibleItem.RefreshContent</c>
            // :135-150 writes the role's description onto the role icon) and, for a festival, the
            // festival's dossier (:81-92). A tile can only ever show ONE tooltip, so folding either
            // into the tile's own reading would promise words the game would never draw: each is a
            // node of its own.
            //
            // CHILDREN of the tile, not stops beside it (owner ruling 2026-08-24) - the same shape the
            // ship tile's role badge already takes (<see cref="ShipRows.Ship"/>): this is a grid the
            // player walks tile by tile, and a badge parked beside each tile would double the walk for
            // a sentence about the tile they are already on.
            List<TooltipChildren.Dossier> badges = new List<TooltipChildren.Dossier>(2);
            TooltipChildren.AddPlain(
                badges,
                item.RoleIcon == null ? null : item.RoleIcon.AgeTransform
            );
            TooltipChildren.Add(badges, item.FestivalIcon);
            if (badges.Count > 0)
            {
                Cell tile = cells[cells.Count - 1];
                tile.Dossiers = badges;
                tile.Key = keyPrefix + "constructible/" + constructible.Name;
            }
        }

        /// <summary>
        /// The item's full name, which is the whole of what a tile says. The grid clips its caption to
        /// fit the tile - "Cerebral ." - so the name is taken from what the tile is FOR rather than
        /// from what the tile says.
        ///
        /// No cost is spoken with it: the tile's prefab is a title label and its badges and draws no
        /// price at all, and the only cost the game ever writes for the item - industry, strategic
        /// resources and the turns they come to - is the tooltip's own cost line, which the tile's
        /// tooltip section already carries for whoever opens it (owner ruling 2026-08-31).
        /// </summary>
        private static string ConstructibleName(StarSystemConstructibleItem item)
        {
            try
            {
                return AgeText.Title(item.GuiConstructible.Title);
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
        ///
        /// Public because the same prefab is the line of the planet card's own constructible list
        /// (<see cref="ES2Access.Screens.PlanetConstructiblesScreen"/>), and two readings of one
        /// game refusal that could disagree are exactly what the shared-helper rule is for.
        /// </summary>
        public static IList<string> ConstructibleFailures(
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

                // The tile itself says nothing about what just happened - it stays exactly as it was,
                // and the queue that grew is a panel away - so this is the only answer a player gets to
                // the key they pressed. Said the moment the mod posts the order, which is the same
                // moment the game plays its own click sound and flies its icon; the tile is only ever
                // reached here after the game's own enablement has been asked, so a refused tile
                // returns before this and keeps the game's silence. Where the front was asked for,
                // the phrase says so: the two keys do different things and the queue they change is a
                // panel away, so hearing the same six words for both leaves the player to go and look.
                Voice.Say(
                    ModStrings.Format(
                        atHead ? ModStrings.QueueQueuedFirst : ModStrings.QueueQueued,
                        ConstructibleName(item)
                    ),
                    true
                );
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
                // Flow control: the queue below is walked line by line, and each line is read for its
                // progress and its buyout buttons.
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
                // Synthetic guard: a queue line is keyed on its Construction and declares no evidence,
                // so this is the whole of its existence test.
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
                    GraphNodes.LabelPart(() => QueueLineName(it)),
                    GraphNodes.ValuePart(() => QueueLineState(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                OnActivate = () => CancelConstruction(it),
                DropKind = QueueKind,
                OnDrop = held => DropInQueue(it, owner, held),
            };
            if (canCarry)
            {
                vtable.OnPickUp = () => Pick(it);
            }

            AgeWidgets.PointAt(vtable, line.AgeTransform);
            string key = keyPrefix + "queue/" + line.Construction.GUID;
            ControlId id = ControlId.For(line.Construction, key);
            List<CardActions.CardAction> buyouts = BuyoutButtons(line);
            if (buyouts.Count == 0)
            {
                // Synthetic: the row stands for the CONSTRUCTION in the queue, and Queued() above -
                // which asks the pooled line whether it is drawn - is what says it is still there.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                CardActions.Emit(builder, key, buyouts);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Take a line out of the queue - the line's own click, which is the game's cancel. Pressed
        /// through MainButton rather than through the panel's handler, so the god-mode branch and the
        /// mid-drag guard the game puts in front of it stay in.
        ///
        /// The queue answers a cancel the way it answers a queueing: the line is gone and no word is
        /// written anywhere, and the cursor is left on whatever the rebuild puts under it - so the
        /// word is the mod's. It is said only where the game cancels OUTRIGHT: a construction with
        /// industry already in it gets the game's own confirmation box instead
        /// (<c>StarSystemQueuePanel.OnCancelConstruction</c> :425-442), which announces itself and can
        /// still be answered no, and in god mode the same button buys the construction out rather than
        /// cancelling it (<c>ConstructionLine.OnCancelCb</c> :378-392).
        /// </summary>
        private static void CancelConstruction(ConstructionLine line)
        {
            try
            {
                if (!Queued(line))
                {
                    return;
                }

                string name = QueueLineName(line);
                bool cancels =
                    !line.Construction.IsAlreadyInvested && !GodGalaxyCursor.IsGuiInGodMode();
                AgeWidgets.Press(line.MainButton);
                if (cancels)
                {
                    Voice.Say(ModStrings.Format(ModStrings.QueueCancelled, name), true);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system panels: cancelling a construction threw: " + e);
            }
        }

        /// <summary>
        /// The buy-out buttons the line draws along its right-hand end, one per currency the game is
        /// willing to consider, each with the price it writes on itself (<see cref="Buyouts.Cost"/>).
        /// A refused one is left DRAWN and switched off with the reason in its own
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
                    // The collected actions are NUMBERED by their place in the list CardActions.Emit
                    // builds, and the number is each node's structural key.
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
                                AgeText.Title(Gui.GetLocalizedTitle("Empire" + it.Resource))
                            ),
                        value: () => Buyouts.Cost(it)
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

        /// <summary>
        /// A queue line's name in full, which is not what the line DRAWS. The line's caption is an
        /// auto-truncating label - the engine chops <c>TranslatedText</c> two characters at a time and
        /// closes it with a period until it fits the column
        /// (<c>AgePrimitiveLabel.ComputeText_AutoTruncateIfNecessary</c> :720-727,
        /// <c>AgeUtils.TruncateString</c> :414-430) - so the drawn string is "Xeno-Industrial." and
        /// speaking it speaks the column width. The assigned <c>Text</c> is still whole, and it is what
        /// the game COMPOSED for this line: a colonization or curiosity line names its planet, and a
        /// per-planet construction names planet and constructible together
        /// (<c>ConstructionLine.Refresh</c> :137-163), none of which the constructible's own title
        /// carries.
        ///
        /// A ship design is the exception, because there the game truncates before assigning: its
        /// title is composed with the label in hand and the revision number clipped to fit
        /// (<c>GuiShipDesign.GetFullTitle</c> :766-781 via <c>AgeUtils.TruncateStringWithSuffix</c>),
        /// so "Big Data Shipy." is already in <c>Text</c>. Asking the same method with no label is the
        /// game's own untruncated answer, revision and all.
        /// </summary>
        private static string QueueLineName(ConstructionLine line)
        {
            try
            {
                GuiShipDesign design = line.GuiConstructible as GuiShipDesign;
                return design != null
                    ? AgeText.Clean(design.GetFullTitle(null))
                    : AgeText.FullLabel(line.Title);
            }
            catch (Exception e)
            {
                Log.Warn("system panels: naming a queue line threw: " + e);
                return AgeText.Label(line.Title);
            }
        }

        /// <summary>The construction this line stands for, picked up.</summary>
        private static CarryItem Pick(ConstructionLine line)
        {
            try
            {
                return new CarryItem(line.Construction, QueueLineName(line), QueueKind);
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
                    return DropResult.Done(ModStrings.Get(ModStrings.DragCancelled));
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
                    ModStrings.Format(ModStrings.DragMovedToPosition, held.Name, index + 1)
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
                // Content: whether the progress figure joins the line's phrase.
                if (line.Progress != null && line.Progress.Visible)
                {
                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.SystemProgress,
                            Mathf.RoundToInt(line.Progress.PercentRight)
                        )
                    );
                }

                // Content: whether the remaining-turns figure joins it.
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
        ///
        /// Two regions, the same shape the constructibles panel gets: the toolbar the game draws across
        /// the top stays one row, and the ships under it are one per row. The ships half always says
        /// something - the empty hangar has a line of its own - so the pair stands or falls with the
        /// toolbar. The toolbar's word is "Actions" (owner-ruled 2026-08-18: these are commands, not
        /// filters), reusing the key the diplomacy band already carries for the same phrase; the ships
        /// keep the shared "Available".
        /// </summary>
        public static void Hangar(GraphBuilder builder, ShipsManagementPanel panel, string keyPrefix)
        {
            try
            {
                // Flow control: the shared ship reader walks the whole panel.
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                string keys = keyPrefix + "hangar";
                Bar.Clear();
                ShipRows.Toolbar(Bar, panel, keys);
                Grid.Clear();
                ShipRows.Ships(Grid, panel, keys, false);

                bool regions = Bar.Count > 0;
                Cells.EmitRegion(
                    builder,
                    keys + "/toolbar",
                    ModStrings.DiplomacyActionsBand,
                    regions,
                    Bar,
                    Cells.AsDrawnRows
                );
                if (regions)
                {
                    builder.SetRegion(keys + "/ships");
                    builder.PushContext(ModStrings.Get(ModStrings.ShipDesignAvailable));
                }

                try
                {
                    if (Grid.Count == 0)
                    {
                        // Synthetic: mod-authored - the mod's own line saying the list is empty.
                        builder.AddItem(Nodes.Synthetic(
                            ControlId.Structural(keys + "/empty"),
                            GraphNodes.Readout(
                                () => ModStrings.Get(ModStrings.SystemHangarEmpty),
                                null,
                                null,
                                null
                            )
                        ));
                    }
                    else
                    {
                        Cells.EmitLinear(builder, Grid);
                    }
                }
                finally
                {
                    if (regions)
                    {
                        builder.PopContext();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system panels: reading the hangar threw: " + e);
            }
        }
    }
}

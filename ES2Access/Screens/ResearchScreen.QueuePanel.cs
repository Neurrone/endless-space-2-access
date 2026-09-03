using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The panels down the left edge - the research status with its queue, and the key to
    /// what the wheel is drawing - and the queueing a technology carried from the wheel ends in.
    /// </summary>
    public sealed partial class ResearchScreen
    {
        // ---- the panels down the left edge ----

        /// <summary>
        /// The two panels the screen adds to the side bar, as one stop with a region each: what the
        /// empire is researching and how fast, then the key that explains the colours.
        ///
        /// They are one stop rather than two because they are drawn as one column against the edge of
        /// the wheel, and because the key is a legend - a place to read rather than a place to go.
        /// </summary>
        private void BuildPanels(GraphBuilder builder, TechnologyScreen window)
        {
            try
            {
                SidePanelsWindow panels = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SidePanelsWindow>(false)
                    : null;
                ResearchStatusSidePanel status =
                    panels == null ? null : panels.GetComponentInChildren<ResearchStatusSidePanel>(true);
                ResearchKeySidePanel key = window.ResearchKeySidePanel;

                builder.BeginStop(StatusStop);
                // Flow control: a region and a context would be opened around nothing, and each panel
                // reading walks a panel of its own.
                if (status != null && AgeWidgets.Visible(status.AgeTransform))
                {
                    builder.SetRegion(QueueRegion);
                    builder.PushContext(ModStrings.Get(ModStrings.ResearchStatusPanel));
                    BuildStatus(builder, status);
                    builder.PopContext();
                }

                // Flow control: same as the status panel above.
                if (key != null && AgeWidgets.Visible(key.AgeTransform))
                {
                    builder.SetRegion(KeyRegion);
                    builder.PushContext(ModStrings.Get(ModStrings.ResearchKeyPanel));
                    BuildKey(builder, key);
                    builder.PopContext();
                }

                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("research: reading the side panels threw: " + e);
            }
        }

        /// <summary>What the empire makes in a turn, what is left over from the last one, and the
        /// queue - or the game's own words for there being no queue.</summary>
        private void BuildStatus(GraphBuilder builder, ResearchStatusSidePanel panel)
        {
            AddDrawnLine(
                builder,
                Group(panel.NetScienceLabel),
                "research:net-science",
                panel.NetScienceTooltip
            );
            AddDrawnLine(builder, panel.ScienceSurplusGroup, "research:science-surplus", null);

            AgeTransform empty = AgeWidgets.Transform(panel.EmptyResearchQueueLabel);
            AddDrawnLine(builder, QueueTitle(Group(panel.EmptyResearchQueueLabel), empty), "research:queue-title", null);

            // The empty-queue label is a wired prefab field, so it exists whether or not the game
            // means it: which branch the panel is in is read off whether the label is DRAWN, the same
            // way the game shows one or the other.
            if (empty != null && AgeWidgets.Visible(empty))
            {
                AddDrawnLine(builder, empty, "research:queue-empty", null);
                return;
            }

            ResearchQueueItem[] items =
                panel.ResearchQueue == null
                    ? null
                    : panel.ResearchQueue.GetComponentsInChildren<ResearchQueueItem>(true);
            int drawn = 0;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                if (Queued(items[i]))
                {
                    drawn++;
                }
            }

            // A technology can only be carried where there is another line to drop it on: with one
            // technology queued the row is not a source, so the player is never put into a mode with
            // nowhere to come out of.
            for (int i = 0; items != null && i < items.Length; i++)
            {
                AddQueueItem(builder, items[i], drawn > 1);
            }
        }

        private static bool Queued(ResearchQueueItem item)
        {
            return item != null
                && item.GuiTechnology != null
                // Which items the queue really holds - the answer also decides whether the panel reads
                // as an empty queue, and the pool keeps retired items around.
                && AgeWidgets.Visible(item.AgeTransform);
        }

        /// <summary>
        /// One technology waiting its turn. Enter takes it out of the queue (<see cref="Dequeue"/>).
        ///
        /// The queue is REORDERED by carrying: Space picks the technology up, Enter on another item
        /// drops it there, and it lands at that item's own position - which is what the game's drag
        /// does (<c>ResearchStatusSidePanel</c> :219-241 posts <c>OrderMoveResearch</c> with the
        /// insertion slot the cursor is over, and <c>ConstructionQueue.Move</c> :156-176 removes and
        /// re-inserts at that index).
        /// </summary>
        private void AddQueueItem(GraphBuilder builder, ResearchQueueItem item, bool canCarry)
        {
            if (!Queued(item))
            {
                return;
            }

            ResearchQueueItem it = item;
            GuiTechnology2 technology = item.GuiTechnology;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(technology.Title)),
                    // Watched: the order that moves a technology up the queue is answered a few
                    // frames after the key that asked for it, and the new position arriving under
                    // the cursor is the only thing that tells the player the move took.
                    GraphNodes.ValuePart(() => QueueItemState(technology)),
                },
                Sections = GraphNodes.Sections(null, item.Tooltip),
                OnActivate = () => Dequeue(it),
                DropKind = QueueKind,
                OnDrop = held => DropInQueue(technology, held),
            };
            if (canCarry)
            {
                vtable.OnPickUp = () => Pick(technology);
            }

            // The row's own state says how many turns the technology has left, so its tooltip's cost
            // panel would say that again.
            GraphNodes.TurnsDrawnOnTheRow(vtable);
            AgeWidgets.Point(vtable, item.Button, item.Tooltip, item.AgeTransform);
            // Synthetic: the row stands for the queued TECHNOLOGY, and Queued() above - which asks the
            // pooled item whether it is drawn - is the honesty about whether it is still queued.
            builder.AddItem(Nodes.Synthetic(
                ControlId.For(technology, "research:queue/" + technology.Name),
                vtable
            ));
        }

        /// <summary>
        /// Take a technology out of the queue - the item's own click, which is the game's dequeue
        /// (<c>ResearchQueueItem.OnActivateCb</c> -&gt; <c>TechnologyScreen.DequeueTechnology</c>
        /// :189-202): no confirmation, and reversible by queueing it again.
        ///
        /// The item vanishes and the cursor is left on whatever the rebuild puts under it, so the
        /// outcome has no words of its own - unlike the wheel, where the dot the player is standing on
        /// says its new state. There is no progress precondition on the game's side: it refuses only a
        /// technology its queue does not hold, and only logs an error for it.
        ///
        /// A refusal is SAID rather than swallowed. The wheel's dots answer a refused Enter through
        /// their own state part (<see cref="Refusal"/> under <c>StateText</c>), which a queue line has
        /// none of, so the line says the same words itself - a key that does nothing and says nothing
        /// is indistinguishable from a key that did not arrive.
        /// </summary>
        private static void Dequeue(ResearchQueueItem item)
        {
            try
            {
                GuiTechnology2 technology = item == null ? null : item.GuiTechnology;
                if (technology == null)
                {
                    return;
                }

                DepartmentOfScience science = Science();
                bool dequeues =
                    Queued(item)
                    && science != null
                    && science.ResearchQueue.Get(technology.TechnologyDefinition) != null;
                if (!dequeues)
                {
                    Voice.Say(Refusal(technology), true);
                    return;
                }

                string name = AgeText.Clean(technology.Title);
                AgeWidgets.Press(item.Button);
                Voice.Say(ModStrings.Format(ModStrings.QueueCancelled, name), true);
            }
            catch (Exception e)
            {
                Log.Warn("research: dequeueing a technology threw: " + e);
            }
        }

        /// <summary>Which cargo a research queue line takes - its own queue's, so a ship or a
        /// population unit cannot be dropped into it.</summary>
        private const string QueueKind = "research-queue";

        /// <summary>The queued technology this line stands for, picked up. The game's own object is
        /// the <c>Construction</c>, which is what the move order names.</summary>
        private static CarryItem Pick(GuiTechnology2 technology)
        {
            try
            {
                DepartmentOfScience science = Science();
                Construction construction =
                    science == null
                        ? null
                        : science.ResearchQueue.Get(technology.TechnologyDefinition);
                return construction == null
                    ? null
                    : new CarryItem(
                        construction,
                        AgeText.Clean(technology.Title),
                        QueueKind
                    );
            }
            catch (Exception e)
            {
                Log.Warn("research: picking a queued technology up threw: " + e);
                return null;
            }
        }

        /// <summary>Put a carried technology at this line's place in the queue - the index the game's
        /// own drag posts when a technology is dropped on this slot.</summary>
        private static DropResult DropInQueue(GuiTechnology2 target, CarryItem held)
        {
            try
            {
                Construction construction = held.Cargo as Construction;
                DepartmentOfScience science = Science();
                if (construction == null || science == null)
                {
                    return DropResult.Refused();
                }

                Construction landing = science.ResearchQueue.Get(target.TechnologyDefinition);
                int index = landing == null ? -1 : science.ResearchQueue.IndexOf(landing);
                if (index < 0 || !science.ResearchQueue.Contains(construction))
                {
                    return DropResult.Refused();
                }

                if (science.ResearchQueue.IndexOf(construction) == index)
                {
                    // Dropped back where it started, which is what the game's own drag does with a
                    // line whose sibling index has not changed: no order, and the drag ended having
                    // moved nothing.
                    return DropResult.Done(ModStrings.Get(ModStrings.DragCancelled));
                }

                MoveInQueue(construction, index);
                return DropResult.Done(
                    ModStrings.Format(ModStrings.DragMovedToPosition, held.Name, index + 1)
                );
            }
            catch (Exception e)
            {
                Log.Warn("research: moving a technology in the queue threw: " + e);
                return DropResult.Refused();
            }
        }

        /// <summary>Where the technology is in the queue and how long is left, the two things the
        /// item draws beside its picture. The number of turns is asked of the game rather than read
        /// off the item, which writes it as a number and a turn symbol.</summary>
        private static string QueueItemState(GuiTechnology2 technology)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                int position = QueuePosition(technology);
                if (position < 0)
                {
                    return null;
                }

                message.ListItem(
                    ModStrings.Format(ModStrings.ResearchQueuePosition, position + 1)
                );
                int turns = Science().GetTechnologyRemainingTurn(technology.TechnologyDefinition);
                if (turns >= 0 && turns < int.MaxValue)
                {
                    message.ListItem(ModStrings.Format(ModStrings.GalaxyTurnsRemaining, turns));
                }
            }
            catch (Exception) { }

            return message.Build();
        }

        /// <summary>
        /// The key panel: the three view switches the game puts at the top of it, then the legend
        /// itself, one line per row it draws under the heading the game drew it under.
        ///
        /// The legend is read off the shape of the panel rather than modelled row by row - every row
        /// is a swatch, a word and a tooltip explaining the state, and there is nothing to do to any
        /// of them. The panel draws its three sections one under another with a heading over each,
        /// and those headings are levels rather than lines: they explain the rows below them and
        /// carry nothing of their own to review.
        /// </summary>
        private void BuildKey(GraphBuilder builder, ResearchKeySidePanel panel)
        {
            AddSwitch(builder, panel.ZoomInToggle, "research:zoom");
            AddSwitch(builder, panel.DisplayUnlocksToggle, "research:unlocks");
            AddSwitch(builder, panel.DisplayKeyToggle, "research:key");

            IList<AgeTransform> rows = AgeWidgets.DrawnChildren(panel.ContentGroup);
            bool section = false;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                string heading = Heading(rows[i]);
                if (heading != null)
                {
                    if (section)
                    {
                        builder.PopContext();
                    }

                    builder.PushContext(heading);
                    section = true;
                    continue;
                }

                AddDrawnLine(builder, rows[i], "research:key/" + i, null);
            }

            if (section)
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// The words a legend row is a HEADING rather than an entry, or nothing at all for an entry.
        ///
        /// The panel draws an entry as a group of two pieces - a swatch and a word - and a heading as
        /// a bare label spanning the panel, so being a label in its own right is what tells the two
        /// apart (measured: the three headings are <c>TechnologiesKeyTitle</c>, <c>LinksKeyTitle</c>
        /// and <c>DeedsKeyTitle</c>, the entries <c>Technology…Group</c>/<c>Link…Group</c>/
        /// <c>Deed…Group</c>). A heading the game hung a sentence on would be a line worth reading in
        /// its own right and stays one; none of these three has any tooltip at all.
        /// </summary>
        private static string Heading(AgeTransform row)
        {
            if (
                // Shape: whether this row counts as a section HEADING, not whether it exists.
                row == null
                || !AgeWidgets.Visible(row)
                || row.GetComponent<AgePrimitiveLabel>() == null
                || AgeWidgets.Raw(row) != null
            )
            {
                return null;
            }

            string text = AgeWidgets.TextOf(row);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static void AddSwitch(GraphBuilder builder, AgeControlToggle toggle, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            // Read when the player asks, not watched: the game drives the zoom switch itself while
            // the view animates - off, on, and unavailable in the space of a third of a second - and
            // a watched box reports every one of them. Nothing but the player ever changes these.
            Settle(vtable);
            AgeWidgets.Point(vtable, it);
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, key), vtable, toggle));
        }

        /// <summary>A line the game draws and the player only reads: a caption and a number, a
        /// swatch and the state it stands for.</summary>
        private static void AddDrawnLine(
            GraphBuilder builder,
            AgeTransform widget,
            string key,
            AgeTooltip tooltip
        )
        {
            if (widget == null)
            {
                return;
            }

            AgeTransform it = widget;
            string text = AgeWidgets.TextOf(widget);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AgeTooltip tip = tooltip ?? AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                },
                Sections = GraphNodes.Sections(null, tip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(widget, key), vtable, widget));
        }

        /// <summary>The caption the game writes over the queue, which it draws as a plain label
        /// beside the list rather than exposing as a field of the panel.</summary>
        private static AgeTransform QueueTitle(AgeTransform group, AgeTransform empty)
        {
            IList<AgeTransform> children = group == null ? null : group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (
                    child != null
                    && !ReferenceEquals(child, empty)
                    && child.GetComponent<AgePrimitiveLabel>() != null
                    // Candidate choice: the first drawn labelled child is the one that names the band.
                    && AgeWidgets.Visible(child)
                )
                {
                    return child;
                }
            }

            return null;
        }

        // ---- queueing ----

        /// <summary>
        /// Queue a technology, or take it out of the queue, through the dot's own toggle - which is
        /// what plays the sound, tells the tutorial that a technology has been chosen, and posts the
        /// order.
        ///
        /// <paramref name="atHead"/> is the game's Alt-click. The game reads the Alt key itself at
        /// the moment the toggle fires, so a player holding it gets the head of the queue from the
        /// game's own code; the move afterwards is for every other way of getting here - an injected
        /// keypress, a keyboard the OS reports no modifier for - and does nothing when the game has
        /// already put it there.
        ///
        /// What happened is said in the mod's own words, the same three the construction queue uses.
        /// In god mode the same toggle unlocks the technology outright instead
        /// (<c>TechnologyItem2.OnToggleCb</c> :734-745), so nothing is claimed about a queue there.
        /// </summary>
        private void Queue(TechnologyItem2 item, bool atHead)
        {
            try
            {
                GuiTechnology2 technology = item.GuiTechnology;
                if (!Operable(technology))
                {
                    return;
                }

                bool queued = QueuePosition(technology) >= 0;
                bool godMode = GodGalaxyCursor.IsGuiInGodMode();
                AgeWidgets.Toggle(item.Toggle);
                if (atHead && !queued)
                {
                    _moveToHead = technology;
                    _moveToHeadFrames = MoveToHeadPatience;
                }

                if (godMode)
                {
                    return;
                }

                // The same words the construction queue answers with, because it is the same act on
                // another screen. The dot's own state word changes under the cursor a moment later and
                // is left alone - it says what the technology IS now, which is a different sentence
                // from what the key just did, and it is only ever heard by a player standing on the
                // dot they pressed.
                string name = AgeText.Clean(technology.Title);
                Voice.Say(
                    ModStrings.Format(
                        queued
                            ? ModStrings.QueueCancelled
                            : atHead
                                ? ModStrings.QueueQueuedFirst
                                : ModStrings.QueueQueued,
                        name
                    ),
                    true
                );
            }
            catch (Exception e)
            {
                Log.Warn("research: queueing a technology threw: " + e);
            }
        }

        /// <summary>How many frames to wait for an order to come back before giving up on moving the
        /// technology it queued to the front - the order goes to the game's own processing and there
        /// is no ticket to wait on when the game posted it.</summary>
        private const int MoveToHeadPatience = 120;

        private GuiTechnology2 _moveToHead;
        private int _moveToHeadFrames;

        /// <summary>The other half of Alt and Enter: once the queue has the technology, put it at the
        /// front - unless the game, reading the Alt key itself, already did.</summary>
        private void FinishMoveToHead()
        {
            if (_moveToHead == null)
            {
                return;
            }

            if (--_moveToHeadFrames <= 0)
            {
                _moveToHead = null;
                return;
            }

            // The QUEUE, not the list the screen draws: the screen adds the technology to its own
            // list the moment the player asks, and the order it posts arrives a few frames later.
            // Moving something the game has not accepted yet moves nothing.
            DepartmentOfScience science = Science();
            ConstructionQueue queue = science == null ? null : science.ResearchQueue;
            Construction construction =
                queue == null ? null : queue.Get(_moveToHead.TechnologyDefinition);
            if (construction == null)
            {
                return;
            }

            _moveToHead = null;
            if (!ReferenceEquals(queue.Peek(), construction))
            {
                MoveInQueue(construction, 0);
            }
        }

        /// <summary>Move a queued technology to another place in the queue - the same order the game
        /// posts when one is dropped somewhere new.</summary>
        private static void MoveInQueue(Construction construction, int index)
        {
            PlayerController player = Gui.GetActivePlayerController();
            player.PostOrder(new OrderMoveResearch(player.Empire.Index, construction.GUID, index));
        }

        /// <summary>Where the technology is in the QUEUE, or -1 for one that is not in it - the game's
        /// own research queue, asked the same way this file's drop and its move-to-head ask it
        /// (:456, :1808). The list the screen draws is a different thing: it holds what the player has
        /// asked for the moment they ask, frames before the order the game posts comes back, which is
        /// exactly the disagreement <see cref="FinishMoveToHead"/> exists to cover.</summary>
        private static int QueuePosition(GuiTechnology2 technology)
        {
            try
            {
                DepartmentOfScience science = Science();
                ConstructionQueue queue = science == null ? null : science.ResearchQueue;
                Construction construction =
                    queue == null || technology == null
                        ? null
                        : queue.Get(technology.TechnologyDefinition);
                return construction == null ? -1 : queue.IndexOf(construction);
            }
            catch (Exception) { }

            return -1;
        }
    }
}

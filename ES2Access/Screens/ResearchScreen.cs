using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The technology wheel: the page F4 opens over whatever the player was looking at, where every
    /// technology in the game is drawn as a dot on one of four quadrants, five rings out from the
    /// middle, with arcs between the dots that mean something.
    ///
    /// A wheel is not a list, and the thing that makes it navigable is that it is not really a wheel
    /// either: it is four quadrants, each of five stages plus the victory ring, each holding a handful
    /// of technologies laid out along the arc. So it is declared as a TREE - quadrant, stage,
    /// technology - and the ring order is the drawn order, clockwise, which is what the angle each dot
    /// was placed at says. Only the stage the player has opened declares its technologies: the wheel
    /// holds 385 of them and rebuilds every frame.
    ///
    /// The camera follows the tree, because on this page the game only DRAWS what is near the middle
    /// of the screen: a technology two rings out does not exist as far as the renderer is concerned
    /// until the viewport has been moved onto it, and a tooltip cannot be drawn for something that is
    /// not. Opening a branch is the player saying "show me this", so opening a quadrant aims the
    /// viewport at that quarter of the wheel and opening a stage aims it at that ring; closing the
    /// last branch puts the whole wheel back on screen. Focusing a technology the renderer has culled
    /// still brings it into view. Nothing here ever fights an animation already running, and the
    /// request is a single slot, so a player walking a ring at speed gets one move, to where they
    /// stopped.
    ///
    /// Typing searches every technology on the wheel rather than the ones on screen
    /// (<see cref="TypeAheadScope"/>): the point of a search here is to reach the one thing you cannot
    /// find, and a search that could only match the stage you already had open would be no help at
    /// all. Landing on a result opens the branch it is buried in.
    ///
    /// One stop on this page has no panel behind it. The game marks the technologies its science
    /// department is recommending by drawing a badge on each dot, scattered around a wheel of 385 -
    /// which is a fine thing to SEE and no way to find anything. So the mod gathers them into a Tab
    /// stop of its own, named with the game's own word for them. That is a deliberate departure from
    /// mirroring what is drawn, approved as such: the badge is on the screen, the list is not. What
    /// the rows are NOT is a summary of the dots - each one IS its dot, seen from elsewhere: focusing
    /// a row takes the view to the technology and points at it, so the game draws the same tooltip it
    /// would on the wheel, and the row carries the same sections. Enter opens the branch it is buried
    /// in and leaves the cursor there.
    ///
    /// What the wheel says and what it does are both the game's: the state words come from the game's
    /// own status strings, queueing replays the dot's own toggle - sound, tutorial event and all - and
    /// the queue's order is moved with the order the game posts when a line is dragged.
    /// </summary>
    public sealed class ResearchScreen : Screen
    {
        private static readonly object StatusStop = "research:status";
        private static readonly object SuggestedStop = "research:suggested";
        private static readonly object TreeStop = "research:tree";
        private static readonly object QueueRegion = "research:region/queue";
        private static readonly object KeyRegion = "research:region/key";

        /// <summary>The clusters the game draws over every page. They are drawn over this one too -
        /// the empire banners, the turn controls and the notification strip all stay on screen while
        /// the wheel is up.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<TechnologyItem2> _technologies = new List<TechnologyItem2>();
        private readonly List<ControlId> _pendingExpand = new List<ControlId>();

        /// <summary>Where the viewport has been asked to look, by the branch the player has just
        /// opened or the dot they have just landed on. A request rather than a move, because a player
        /// holding an arrow down walks a whole ring in the time one zoom takes, and only the last of
        /// those is worth animating to. One slot: the target that goes with the aim.</summary>
        private ResearchCamera.Aim _cameraAim;
        private TechnologyQuadrantItem _cameraQuadrant;
        private BaseTechnologyStageItem _cameraStage;
        private TechnologyItem2 _cameraTechnology;

        public override string Key
        {
            get { return "screen.research"; }
        }

        /// <summary>Above the view levels the wheel is drawn over, and below the panel a planet card
        /// slides out: it is a screen of the game's own, not a level of the galaxy.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = ScreenTitle();
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenResearch) : title;
            }
        }

        /// <summary>The wheel, which is what the player opened the page for. The panels down the left
        /// edge are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return TreeStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the same branch open
        /// and the same technology under the cursor - opening the wheel again to look at the thing
        /// you were just looking at should not mean walking back down to it.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the screen, which is also what the page's own exit
        /// does. The type-ahead layer takes the key away only while a search is up, and puts it back
        /// itself.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        public override bool IsActive()
        {
            try
            {
                TechnologyScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null && !gui.IsAnyModalVisible && !gui.IsInLoadingWindow;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _hud.Baseline();
        }

        public override void OnPop()
        {
            _hud.Forget();
            Look(ResearchCamera.Aim.None, null, null, null);
            _pendingExpand.Clear();
            // A locate the page never got to answer goes with the page: the player closed it, and a
            // technology remembered from that visit has no business moving the cursor on the next one.
            ResearchLocate.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
            FollowTheGame();
            MoveCamera();
        }

        /// <summary>
        /// Land on the technology the GAME has just gone and looked at
        /// (<see cref="ResearchLocate"/>): the same landing a search makes, so the branch it is buried
        /// in is opened and the cursor is left on the dot.
        ///
        /// Here rather than on arrival because the two ways in are the same thing: the page opens at
        /// the technology, or it was already open and the view moved to it. Both leave the request
        /// waiting, and the first frame this page has the keyboard is when it can be answered - which
        /// is also why a locate made under a tutorial popup is not lost, only held.
        /// </summary>
        private void FollowTheGame()
        {
            GuiTechnology2 wanted = ResearchLocate.Take();
            if (wanted != null)
            {
                Jump(wanted);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            TechnologyScreen window = Window();
            if (window == null)
            {
                return;
            }

            ApplyPendingExpansions(builder);

            // Down and across the screen: the empire's banners along the top with this screen's own
            // title beside them, the two panels down the
            // left edge, the wheel in the middle, then the right-hand edge and the turn controls -
            // the same order every page under these clusters declares them in. The recommended
            // technologies sit between the panels and the wheel because that is where they point:
            // they are a way into the wheel rather than a panel of their own.
            _hud.Top(builder);
            BuildPanels(builder, window);
            BuildSuggestions(builder, window);
            BuildWheel(builder, window);
            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

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

            AgeTransform empty = Widget(panel.EmptyResearchQueueLabel);
            AddDrawnLine(builder, QueueTitle(Group(panel.EmptyResearchQueueLabel), empty), "research:queue-title", null);

            if (empty != null)
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
                    return DropResult.Done(ModStrings.Get(ModStrings.CarryCancelled));
                }

                MoveInQueue(construction, index);
                return DropResult.Done(
                    ModStrings.Format(ModStrings.CarryMovedToPosition, held.Name, index + 1)
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

        // ---- the technologies the game is recommending ----

        /// <summary>
        /// The technologies the empire's science department is putting forward this turn, as a stop of
        /// their own.
        ///
        /// The game shows these by badging the dots where they happen to lie, which tells a player who
        /// can see the wheel where to look and tells everyone else nothing at all. The list is the
        /// game's own (the same one the wheel badges from, recomputed at the start of every turn and
        /// whenever something is unlocked), and it is declared only while the game has something to
        /// recommend - an empty stop would be a Tab press into silence.
        /// </summary>
        private void BuildSuggestions(GraphBuilder builder, TechnologyScreen window)
        {
            try
            {
                List<GuiTechnology2> suggested = window.SuggestedGuiTechnologies;
                if (suggested == null || suggested.Count == 0)
                {
                    return;
                }

                builder.BeginStop(SuggestedStop);
                string title = SuggestedWord();
                bool named = !string.IsNullOrEmpty(title);
                if (named)
                {
                    builder.PushContext(title);
                }

                for (int i = 0; i < suggested.Count; i++)
                {
                    // A list of four, scanned for a repeat: a control declared twice throws out of
                    // the build and takes the WHOLE screen down with it, which is far too high a
                    // price for trusting a list the mod does not own.
                    if (!Repeat(suggested, i))
                    {
                        AddSuggestion(builder, window, suggested[i]);
                    }
                }

                if (named)
                {
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: reading the recommended technologies threw: " + e);
            }
        }

        /// <summary>Whether the same technology is already further up the recommended list.</summary>
        private static bool Repeat(List<GuiTechnology2> suggested, int index)
        {
            GuiTechnology2 technology = suggested[index];
            for (int i = 0; i < index; i++)
            {
                if (suggested[i] != null && technology != null && suggested[i].Name == technology.Name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One recommendation, which is the technology's own dot read from somewhere else on the
        /// page: the same name, state, cost and turns, the same arcs, and the same tooltip.
        ///
        /// Focusing the row moves the view onto the dot and puts the pointer there, exactly as
        /// focusing the dot does - not for the look of it, but because the wheel does not DRAW a
        /// technology that is off screen, and a tooltip cannot be drawn for something that is not.
        /// Enter is the row's own job: it opens the quadrant and the stage the technology is buried
        /// in and leaves the cursor on the dot.
        /// </summary>
        private void AddSuggestion(
            GraphBuilder builder,
            TechnologyScreen window,
            GuiTechnology2 technology
        )
        {
            if (technology == null)
            {
                return;
            }

            GuiTechnology2 it = technology;
            TechnologyItem2 dot = Dot(window, technology);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(it.Title)),
                    GraphNodes.ValuePart(() => StateWord(it)),
                    Value(() => TechnologyState(it)),
                    Value(() => Relationships(it)),
                },
                Sections = dot == null
                    ? null
                    : GraphNodes.Sections(GraphNodes.TooltipSection(dot.Tooltip)),
                OnActivate = () => Jump(it),
            };
            if (dot != null)
            {
                ShowDot(vtable, dot);
            }
            // Keyed on nothing the wheel names: a row and a dot that named the same technology would
            // be one control to the cursor, and pressing Enter here would teleport the player back
            // into this list the moment the dot they landed on rebuilt.
            builder.AddItem(Nodes.Synthetic(
                ControlId.Structural("research:suggested/" + technology.Name),
                vtable
            ));
        }

        /// <summary>The game's own word for a technology it is putting forward - the one it writes on
        /// the badge it draws over the dot. Null rather than a raw key when the corpus has no
        /// translation for it, so the list is simply unnamed instead of reading out machinery.
        /// </summary>
        private static string SuggestedWord()
        {
            try
            {
                string text = AgeText.Clean(Gui.Localize(SuggestedTitleKey));
                return string.IsNullOrEmpty(text) || text[0] == '%' ? null : text;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string SuggestedTitleKey = "%SuggestedItemTitle";

        /// <summary>Whether the wheel is badging this technology as one to take next.</summary>
        private static bool Suggested(GuiTechnology2 technology)
        {
            try
            {
                TechnologyScreen window = Window();
                List<GuiTechnology2> suggested =
                    window == null ? null : window.SuggestedGuiTechnologies;
                for (int i = 0; suggested != null && i < suggested.Count; i++)
                {
                    if (ReferenceEquals(suggested[i], technology))
                    {
                        return true;
                    }
                }
            }
            catch (Exception) { }

            return false;
        }

        /// <summary>Take the player to a technology wherever it is on the wheel - the same landing a
        /// search makes, from the other end: open the branch, then ask for the dot.</summary>
        private void Jump(GuiTechnology2 technology)
        {
            try
            {
                List<TechnologyItem2> items = new List<TechnologyItem2>();
                List<ControlId> quadrants = new List<ControlId>();
                List<ControlId> stages = new List<ControlId>();
                WheelIndex(items, quadrants, stages);
                for (int i = 0; i < items.Count; i++)
                {
                    if (!ReferenceEquals(items[i].GuiTechnology, technology))
                    {
                        continue;
                    }

                    ModEntry.Navigator.FocusNode(Reveal(items[i], quadrants[i], stages[i]));
                    return;
                }

                Log.Warn("research: the wheel is not drawing " + technology.Name);
            }
            catch (Exception e)
            {
                Log.Warn("research: jumping to a technology threw: " + e);
            }
        }

        // ---- the wheel ----

        /// <summary>
        /// The four quadrants in the order they are drawn, clockwise from the top, each opening into
        /// its stages and each stage into the technologies along its arc.
        ///
        /// A collapsed branch declares nothing at all - not even its children's names - which is what
        /// keeps a 385-technology page inside a per-frame rebuild.
        /// </summary>
        private void BuildWheel(GraphBuilder builder, TechnologyScreen window)
        {
            AgeTransform container = window.TechnologyQuadrantsContainer;
            if (container == null || container.Children == null)
            {
                return;
            }

            builder.BeginStop(TreeStop);
            builder.PushContext(ModStrings.Get(ModStrings.ResearchTreePanel));
            try
            {
                for (int i = 0; i < container.Children.Count; i++)
                {
                    TechnologyQuadrantItem quadrant =
                        container.Children[i].GetComponent<TechnologyQuadrantItem>();
                    if (quadrant == null)
                    {
                        continue;
                    }

                    ControlId id = QuadrantId(i);
                    // Synthetic: the wheel's quadrants are a level the mod invented over the game's own
                    // layout; the scrape above is what says how many there are.
                    builder.BeginGroup(Nodes.Synthetic(id, QuadrantVtable(builder, quadrant, id)));
                    if (builder.IsExpanded(id))
                    {
                        BuildStages(builder, quadrant, i);
                    }

                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: reading the wheel threw: " + e);
            }

            builder.PopContext();
        }

        private void BuildStages(GraphBuilder builder, TechnologyQuadrantItem quadrant, int index)
        {
            IList<AgeTransform> stages = quadrant.TechnologyStagesContainer.Children;
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                BaseTechnologyStageItem stage = stages[i].GetComponent<BaseTechnologyStageItem>();
                if (stage == null || stage.GuiTechnologyStage == null)
                {
                    continue;
                }

                ControlId id = StageId(index, i);
                // Synthetic: the same invented level one step down, enumerated from the stages the
                // quadrant holds.
                builder.BeginGroup(Nodes.Synthetic(id, StageVtable(builder, quadrant, stage, id)));
                if (builder.IsExpanded(id))
                {
                    // A stage unlocks things of its own, the same way a dot does, so the ring's
                    // contents are the stage's actions and its unlocks are the tooltip region after
                    // them - the split every other unlock strip is read with.
                    string key = StageUnlockKey(stage);
                    object outer = TooltipChildren.Actions(builder, key);

                    // The deed first: it is drawn on the ring like the dots are, and it is the one
                    // thing on the ring that is not a technology.
                    BuildDeed(builder, stage, index, i);
                    BuildTechnologies(builder, stage);
                    TooltipChildren.Emit(builder, key, StageUnlocks(stage), outer);
                }

                builder.EndGroup();
            }
        }

        private void BuildTechnologies(GraphBuilder builder, BaseTechnologyStageItem stage)
        {
            Technologies(stage, _technologies);
            for (int i = 0; i < _technologies.Count; i++)
            {
                TechnologyItem2 item = _technologies[i];
                ControlId id = TechnologyId(item);
                List<TooltipChildren.Dossier> unlocks = Unlocks(item);
                if (unlocks.Count == 0)
                {
                    // Synthetic: a technology node stands for the technology, and Technologies()
                    // above is the walk that says which of them the wheel is showing.
                    builder.AddItem(Nodes.Synthetic(id, TechnologyVtable(item)));
                    continue;
                }

                // A dot that unlocks something is a branch: the dot keeps its own name, its own
                // state, its own click and its own dossier, and each thing it unlocks becomes a node
                // under it carrying the game's full page about that thing - which the wheel shows a
                // mouse by revealing a strip of icons under the dot on hover and nowhere else.
                builder.BeginGroup(Nodes.Synthetic(id, TechnologyVtable(item)));
                if (builder.IsExpanded(id))
                {
                    TooltipChildren.Emit(
                        builder,
                        UnlockKey(item),
                        unlocks,
                        TooltipChildren.Actions(builder, UnlockKey(item))
                    );
                }

                builder.EndGroup();
            }
        }

        /// <summary>
        /// The things a technology unlocks, as the wheel DRAWS them.
        ///
        /// The dot's own tooltip lists them by name and effect and stops there - it has no cost panel
        /// at all (the game's data says so: the <c>TechnologyUnlockEmbedded</c> class is header,
        /// effects and nothing else). What a mouse gets instead is a strip of icons the wheel reveals
        /// under a hovered dot, each carrying the unlocked thing's FULL dossier - description, effects,
        /// cost, upkeep, political impact. Those icons are what these nodes point at, so the keyboard
        /// gets the page the mouse gets.
        ///
        /// One node per DRAWN icon. A technology can carry an unlock its data hides from this empire -
        /// one gated on another affinity - and the wheel binds no icon for it, so nothing here declares
        /// it either: what the picture is not showing is not said.
        /// </summary>
        private static List<TooltipChildren.Dossier> Unlocks(TechnologyItem2 item)
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(3);
            AddAffinity(found, item);
            try
            {
                AgeTransform container = item.TechnologyUnlocksContainer;
                IList<AgeTransform> icons = container == null ? null : container.Children;
                for (int i = 0; icons != null && i < icons.Count; i++)
                {
                    // The strip is a subtree the wheel REVEALS on hover - it sits at alpha 0 until the
                    // dot is hovered - so the visibility asked here is the game's own Visible flag and
                    // never the transparency: an icon judged by alpha would exist only while the mouse
                    // was already on the dot, which is the one time a keyboard player is not there.
                    AgeTransform icon = icons[i];
                    if (icon != null && icon.Visible)
                    {
                        // Carrier-less, so the node is Synthetic and no chain test is asked: see
                        // StageUnlocks below for the measurement and the reason it is the same here -
                        // the dot's strip hangs under DetailedGroup, which the wheel switches off
                        // except while the dot is hovered.
                        TooltipChildren.AddRevealed(found, AgeWidgets.Raw(icon), icon);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: reading a technology's unlocks threw: " + e);
            }

            return found;
        }

        private static string UnlockKey(TechnologyItem2 item)
        {
            GuiTechnology2 technology = item == null ? null : item.GuiTechnology;
            return "research:technology/" + (technology == null ? "?" : technology.Name.ToString());
        }

        /// <summary>
        /// The things a whole STAGE unlocks, as the wheel draws them.
        ///
        /// The game hangs the same strip of icons on a stage that it hangs on a dot, one level up
        /// (<c>TechnologyStageItem.BindUnlocks</c> fills the stage's own
        /// <c>TechnologyUnlocksContainer</c> from <c>GuiTechnologyStage.TechnologyUnlocks</c>), and the
        /// reading is the same split <see cref="Unlocks"/> gets: the stage's own tooltip names them and
        /// their effects in a line each, and these nodes carry each one's FULL dossier - description,
        /// effects, cost, upkeep, political impact.
        ///
        /// The test asked HERE is the ICON's own <c>Visible</c> flag rather than a chain-walking one,
        /// because the wheel hides the strip's CONTAINER at every zoom but the outermost
        /// (<c>TechnologyStageItem.Blend</c> ties it to the stage-name group, which only appears when
        /// the wheel is zoomed out) while leaving every icon bound, visible and drawable - measured:
        /// aiming at an icon of a hidden container still draws its whole dossier.
        ///
        /// So these are collected through <see cref="TooltipChildren.AddRevealed"/>, which names no
        /// carrier and leaves the node <see cref="UI.Nodes.Synthetic"/>: the icon is still what the
        /// pointer is aimed at, and this walk - "which icons has the wheel bound?" - is what vouches
        /// for the nodes, the way it does for every other synthetic node. The renderer is not the
        /// source of truth for a strip whose whole design is to be invisible until a mouse arrives.
        ///
        /// Measured on the live wheel 2026-08-27 with Display Unlocks ticked and at BOTH states of the
        /// Zoom In toggle: <c>TechnologyUnlocksContainer</c> reads <c>Visible=False, Alpha=0</c> while
        /// its icons read <c>Visible=True, Alpha=1</c>, and aiming at an icon of that hidden container
        /// still draws its whole dossier. Declared with a carrier, <see cref="UI.NodeGate"/> walked the
        /// icon's ancestry and dropped every one of them: <c>DevProbe.GateDiff()</c> with Military I
        /// expanded named all seven
        /// <c>research:stage-unlock/TechnologyStageDefinitionMilitary1/tooltip/N</c> under
        /// <c>onlyUngated</c>, with the drop log reading
        /// <c>ancestor not visible (TechnologyUnlocksContainer)</c>.
        ///
        /// The hub in the middle of the wheel is a <c>VictoryTechnologyStageItem</c>, which has no
        /// strip at all: the cast answers null and the stage declares no unlocks.
        /// </summary>
        private static List<TooltipChildren.Dossier> StageUnlocks(BaseTechnologyStageItem stage)
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(8);
            try
            {
                TechnologyStageItem ring = stage as TechnologyStageItem;
                AgeTransform container = ring == null ? null : ring.TechnologyUnlocksContainer;
                IList<AgeTransform> icons = container == null ? null : container.Children;
                for (int i = 0; icons != null && i < icons.Count; i++)
                {
                    AgeTransform icon = icons[i];
                    if (icon != null && icon.Visible)
                    {
                        TooltipChildren.AddRevealed(found, AgeWidgets.Raw(icon), icon);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: reading a stage's unlocks threw: " + e);
            }

            return found;
        }

        /// <summary>Named off the stage DEFINITION rather than off its place on the wheel, so the
        /// region and its nodes keep their identity when the wheel is rebuilt.</summary>
        private static string StageUnlockKey(BaseTechnologyStageItem stage)
        {
            GuiTechnologyStage guiStage = stage == null ? null : stage.GuiTechnologyStage;
            return "research:stage-unlock/" + (guiStage == null ? "?" : guiStage.Name.ToString());
        }

        /// <summary>One quarter of the wheel. Its own words about itself are in the tooltip the game
        /// hangs on the title it writes across the quadrant.</summary>
        private NodeVtable QuadrantVtable(
            GraphBuilder builder,
            TechnologyQuadrantItem quadrant,
            ControlId id
        )
        {
            TechnologyQuadrantItem it = quadrant;
            AgeTransform title = Widget(quadrant.QuadrantTitle);
            AgeTooltip tooltip = AgeWidgets.Raw(title);
            NodeVtable vtable = GraphNodes.Group(() => QuadrantTitle(it), null, tooltip);
            Hover(vtable, title, tooltip);
            Branch(builder, vtable, id, ResearchCamera.Level.Quadrant, quadrant, null);
            return vtable;
        }

        /// <summary>One ring of one quadrant: what the game calls it, how much of it is done, and -
        /// while it is still locked - the game's own sentence about what would unlock it.</summary>
        private NodeVtable StageVtable(
            GraphBuilder builder,
            TechnologyQuadrantItem quadrant,
            BaseTechnologyStageItem stage,
            ControlId id
        )
        {
            BaseTechnologyStageItem it = stage;
            AgeTransform name = stage.StageNameGroup;
            AgeTooltip tooltip = stage.StageTooltip;
            NodeVtable vtable = GraphNodes.Group(() => StageTitle(it), null, tooltip);
            // The lock sentence is the game.s own, written on markers the stage draws around its ring
            // and nowhere the ring itself says: the row went and got it, so the row says it - declared
            // as a section, so the same words reach the review buffer exactly once.
            vtable.Sections = GraphNodes.SpokenSections(() => StageLockLines(it), tooltip);
            Hover(vtable, name, tooltip);
            Branch(builder, vtable, id, ResearchCamera.Level.Stage, quadrant, stage);
            return vtable;
        }

        /// <summary>
        /// The deed the game hangs on a stage's ring: a race between the empires that only one of them
        /// wins, drawn as a marker with a progress track around it.
        ///
        /// One node, read-only - there is nothing to do to a deed from here, and the game's own button
        /// on it is a debug affordance. It says what the game says: the deed's title, the game's word
        /// for what a deed is, the state its colour stands for and, once someone else has taken it,
        /// who - which the game draws as their logo and so says to nobody else. When the game has no
        /// deed for the stage it draws no marker, and there is no node.
        /// </summary>
        private void BuildDeed(
            GraphBuilder builder,
            BaseTechnologyStageItem stage,
            int quadrant,
            int index
        )
        {
            try
            {
                TechnologyStageItem ring = stage as TechnologyStageItem;
                DeedItem2 marker = ring == null ? null : ring.DeedItem;
                // Synthetic guard: the deed is read out of the quest behind the marker, so the node
                // declares no evidence and the gate has nothing to ask.
                if (marker == null || !AgeWidgets.Visible(marker.AgeTransform))
                {
                    return;
                }

                // A marker the mod cannot read the quest behind is a node with nothing to say, which
                // is worse than no node: declare it only once there are words for it.
                if (Deed(marker) == null)
                {
                    return;
                }

                DeedItem2 it = marker;
                AgeTooltip tooltip = marker.Tooltip;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => DeedTitle(it)),
                        Value(() => DeedKind(it)),
                        Value(() => DeedState(it)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };
                if (tooltip != null)
                {
                    AgeWidgets.PointAt(vtable, tooltip.AgeTransform);
                }

                // Synthetic: a deed is read out of the quest behind the marker, which the wheel
                // draws as a dot with no words of its own.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("research:deed/" + quadrant + "/" + index),
                    vtable
                ));
            }
            catch (Exception e)
            {
                Log.Warn("research: reading a stage's deed threw: " + e);
            }
        }

        /// <summary>
        /// One technology: what it is called, what state the game has it in, what it costs and how
        /// long it would take, and where it sits in the queue once it is in one.
        ///
        /// Enter is the dot's own click - queue it, or take it out of the queue when it is already in
        /// one - and Alt and Enter is the game's own Alt-click, which puts it at the FRONT of the
        /// queue.
        ///
        /// The arcs the wheel draws from this dot to others are part of what the dot is called: which
        /// technologies it unlocks and is unlocked by, which it makes cheaper and is made cheaper by,
        /// and which it rules out. A line between two dots is a fact only to somebody who can see which
        /// two dots it joins, and it is the fact that decides what to research next - so it is said on
        /// landing rather than left to a review key (owner ruling, all five kinds). It is one
        /// announcement part rather than a section of its own so that the buffer carries it once: the
        /// buffer's head is read off the parts, and a section saying the same thing would be the same
        /// arcs twice.
        /// </summary>
        private NodeVtable TechnologyVtable(TechnologyItem2 item)
        {
            TechnologyItem2 it = item;
            GuiTechnology2 technology = item.GuiTechnology;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(technology.Title)),
                    // No "unavailable" of the mod's own: the game has a word for every state a
                    // technology can be in, one of them is "Not available", and hearing both is
                    // hearing the same thing twice.
                    GraphNodes.ValuePart(() => StateWord(technology)),
                    // The badge the wheel draws over a recommended dot, in the game's own word for
                    // it - beside the state rather than in the buffer, because it is the reason a
                    // player would pick this dot over the one next to it.
                    GraphNodes.ValuePart(
                        () => Suggested(technology) ? SuggestedWord() : null
                    ),
                    Value(() => TechnologyState(technology)),
                    Value(() => Relationships(technology)),
                },
                Sections = GraphNodes.Sections(GraphNodes.TooltipSection(item.Tooltip)),
                OnActivate = () => Queue(it, false),
                OnAlternate = () => Queue(it, true),
                // A technology the game will not take says why, in its own words, and does nothing
                // else. Anything it WOULD take reports itself through the state word instead, which
                // changes under the cursor as the order comes back.
                StateText = () => Operable(technology) ? null : Refusal(technology),
            };
            // The one gesture on a dot that is not the plain click, said at the end of the buffer
            // because the wheel writes it nowhere and the queue it changes is a screen away.
            NodeHints.Add(vtable, ModStrings.HintQueueFirst, UiActions.Alternate);
            ShowDot(vtable, item);
            return vtable;
        }

        /// <summary>
        /// The marker the wheel draws on a technology that unlocks something only ONE affinity gets -
        /// the player's own where this technology has one for it (<c>TechnologyItem2.CommonBind</c>
        /// :290-316 prefers it). The dot draws it as an icon and names the affinity only in the
        /// icon's own tooltip, which is the one place those words exist, so that tooltip joins the
        /// dot's own; the group is hidden on every technology with no such unlock, which is the gate.
        ///
        /// It is a BADGE on the dot, not the dot's own description: a second hover target on the same
        /// line, which the dot cannot raise because it points at the technology's own dossier. It was a
        /// reviewed section here - words the dot promised and the game only ever drew for the dossier -
        /// and is now an ENTRY of its own, in front of the things the technology unlocks, which is the
        /// order the wheel draws the two in. Two thirds of the wheel's 385 technologies carry one
        /// (measured, 254), so this is also what keeps the same ten words out of the front of nearly
        /// every dot.
        /// </summary>
        private static void AddAffinity(List<TooltipChildren.Dossier> into, TechnologyItem2 item)
        {
            try
            {
                // Content: whether the affinity badge belongs in the dot's reading at all.
                if (item.AffinityGroup != null && AgeWidgets.Visible(item.AffinityGroup))
                {
                    TooltipChildren.AddPlain(into, item.AffinityTooltip, item.AffinityGroup);
                    TooltipChildren.Add(into, item.AffinityTooltip, item.AffinityGroup);
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: reading a dot's affinity badge threw: " + e);
            }
        }

        /// <summary>
        /// Look at a technology's dot and put the pointer on it - what focusing the dot itself does,
        /// and what focusing its row in the recommended list does too.
        ///
        /// Both halves are load-bearing and neither is decoration. The wheel does not draw a
        /// technology that is off screen, so the camera request is what makes the dot exist for the
        /// renderer; the pointer is what makes the game draw its tooltip beside it, which is the only
        /// place a Class-backed tooltip's words ever exist.
        ///
        /// It also SAYS which tooltip that is. A node makes two promises about a tooltip - the words are
        /// reviewable (<c>PointsAt</c>) and focusing raises them (<c>OnFocusVisual</c>) - and this raised
        /// one without ever naming it, which reads to every parity bucket as a node with no tooltip at
        /// all. The dot and the recommended row have raised this tooltip since they were written; naming
        /// it changes nothing the player hears and everything the audit can see.
        /// </summary>
        private void ShowDot(NodeVtable vtable, TechnologyItem2 item)
        {
            TechnologyItem2 it = item;
            vtable.PointsAt = () => it.Tooltip;
            vtable.OnFocusVisual = () =>
            {
                Look(ResearchCamera.Aim.Technology, null, null, it);
                PointerFocus.MoveToToggle(it.Toggle, it.Tooltip, it.AgeTransform);
            };
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
        }

        /// <summary>The dot the wheel draws for a technology, through the screen's own index of them -
        /// which is how the game finds it to focus it, and costs one lookup rather than a walk of all
        /// 385.</summary>
        private static TechnologyItem2 Dot(TechnologyScreen window, GuiTechnology2 technology)
        {
            try
            {
                TechnologyItem2 item;
                return window.TechnologyItemByGuiTechnology.TryGetValue(technology, out item)
                    ? item
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Make the game look at what the cursor is on, so a sighted watcher can follow -
        /// and so the tooltip the game only draws under the pointer exists at all.
        ///
        /// The pointer goes to whatever the TOOLTIP hangs on rather than to the widget the words were
        /// read off, because the engine re-derives the tooltip it draws from the transform it is told
        /// the mouse is over: a quadrant's title carries its own explanation and the two are the same
        /// transform, but a stage keeps its dossier on a transform of its own and aiming at the drawn
        /// name then drew nothing at all, leaving the stage's dossier out of its review buffer
        /// (<see cref="AgeWidgets.PointAt"/>).</summary>
        private static void Hover(NodeVtable vtable, AgeTransform widget, AgeTooltip tooltip)
        {
            AgeWidgets.PointAt(vtable, widget, tooltip);
        }

        /// <summary>
        /// Opening and closing a branch of the wheel, which is also how the viewport is moved.
        ///
        /// These hooks REPLACE the engine's own expansion bookkeeping rather than adding to it, so
        /// the set has to be flipped here or the branch would refuse to stay open. What is added is
        /// the camera: the player has just said "show me this", or "I am done with this", and the
        /// view goes where they now are.
        /// </summary>
        private void Branch(
            GraphBuilder builder,
            NodeVtable vtable,
            ControlId id,
            ResearchCamera.Level level,
            TechnologyQuadrantItem quadrant,
            BaseTechnologyStageItem stage
        )
        {
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId branch = id;
            ResearchCamera.Level depth = level;
            TechnologyQuadrantItem quarter = quadrant;
            BaseTechnologyStageItem ring = stage;
            vtable.OnExpand = () =>
            {
                if (expansion != null)
                {
                    expansion.Add(branch);
                }

                Look(ResearchCamera.ForExpansion(depth, true), quarter, ring, null);
            };
            vtable.OnCollapse = () =>
            {
                if (expansion != null)
                {
                    expansion.Remove(branch);
                }

                Look(ResearchCamera.ForExpansion(depth, false), quarter, ring, null);
            };
        }

        /// <summary>The one camera request, overwritten rather than queued: the last thing the player
        /// asked to see is the only one worth an animation.</summary>
        private void Look(
            ResearchCamera.Aim aim,
            TechnologyQuadrantItem quadrant,
            BaseTechnologyStageItem stage,
            TechnologyItem2 technology
        )
        {
            _cameraAim = aim;
            _cameraQuadrant = quadrant;
            _cameraStage = stage;
            _cameraTechnology = technology;
        }

        // ---- what a technology says ----

        /// <summary>The game's own word for the state a technology is in - the same string it writes
        /// into the technology's tooltip.</summary>
        private static string StateWord(GuiTechnology2 technology)
        {
            try
            {
                return AgeText.Clean(
                    Gui.Localize("%TechnologyStatus" + State(technology) + "Title")
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What it would cost and where it is in the queue - the two things the wheel puts
        /// on a dot. The cost is drawn in the dot's own tooltip ("Cost: 131 Science") and the queue
        /// position on the dot itself (<c>TechnologyItem2.PositionInQueueGroup</c>, shown only while
        /// the technology is queued or in progress, which is the same condition as
        /// <see cref="QueuePosition"/> answering). No turn count: nothing on this page draws one
        /// (<see cref="ResearchText.Progress"/>).</summary>
        private static string TechnologyState(GuiTechnology2 technology)
        {
            try
            {
                string costs = State(technology) == ScienceConstructibleElement.State.Researched
                    ? null
                    : AgeText.Clean(technology.GetFinalCostsString(Gui.PlayerEmpire));
                return ResearchText.Progress(costs, QueuePosition(technology));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Why the game is refusing a technology: the state it is in, and for one it has
        /// ruled out for good, the reasons it collected while ruling it out.</summary>
        private static string Refusal(GuiTechnology2 technology)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                message.Fragment(StateWord(technology));
                List<FailureInfo> failures = technology.FailureInfos;
                for (int i = 0; failures != null && i < failures.Count; i++)
                {
                    string text = AgeText.Clean(
                        Gui.FormatFailure(string.Empty, failures[i].Flag.ToString())
                    );
                    message.ListItem(text);
                }
            }
            catch (Exception) { }

            return message.Build();
        }

        // ---- what a deed says ----

        /// <summary>What the game calls the deed - the quest's own title, localized.</summary>
        private static string DeedTitle(DeedItem2 marker)
        {
            try
            {
                GuiDeed deed = Deed(marker);
                return deed == null ? null : AgeText.Clean(deed.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own word for the kind of thing this is, taken from the category the
        /// quest is filed under rather than invented: "Deed".</summary>
        private static string DeedKind(DeedItem2 marker)
        {
            try
            {
                GuiDeed deed = Deed(marker);
                string category = deed == null ? null : deed.Category;
                if (string.IsNullOrEmpty(category))
                {
                    return null;
                }

                string word = AgeText.Clean(Gui.Localize("%" + category + "Title"));
                return string.IsNullOrEmpty(word) || word[0] == '%' ? null : word;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the marker's colour says: one of the four words the key panel gives the deed states,
        /// and - for a deed another empire has already taken - which empire that was.
        ///
        /// The game draws the winner as their logo beside the marker, so this is the one part of a
        /// deed the mod has to put into words of its own.
        /// </summary>
        private static string DeedState(DeedItem2 marker)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                GuiDeed deed = Deed(marker);
                if (deed == null)
                {
                    return null;
                }

                string state = ResearchText.DeedStateName(
                    DeedVisible(deed),
                    deed.IsDeedAvailable(),
                    Progress(deed.State)
                );
                message.Fragment(AgeText.Clean(Gui.Localize("%DeedState" + state + "Title")));
                if (deed.State == QuestState.Failed)
                {
                    message.ListItem(ResearchText.DeedWinner(Winner(deed)));
                }
            }
            catch (Exception) { }

            return message.Build();
        }

        /// <summary>Whether the game is showing this deed at all: an empire has to have researched the
        /// stage it hangs on before anyone learns what the race is for.</summary>
        private static bool DeedVisible(GuiDeed deed)
        {
            try
            {
                return GodGalaxyCursor.IsGuiInGodMode() || deed.IsDeedVisible();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Which empire completed this deed, out of the one quest every empire is racing on
        /// its own copy of - the same search the marker makes to pick the logo it draws.</summary>
        private static string Winner(GuiDeed deed)
        {
            try
            {
                IQuestManagementService quests =
                    Amplitude.Unity.Framework.Services.GetService<IQuestManagementService>();
                List<Quest> racing =
                    quests == null
                        ? null
                        : quests.GetQuestsByInstanceId(deed.Quest.QuestInstanceID);
                for (int i = 0; racing != null && i < racing.Count; i++)
                {
                    if (racing[i].State != QuestState.Completed)
                    {
                        continue;
                    }

                    GuiEmpire empire = Gui.GuiWrapperProviderService.GetGuiEmpire(
                        racing[i].EmpireIndex
                    );
                    return empire == null ? null : AgeText.Clean(empire.Title);
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: finding a deed's winner threw: " + e);
            }

            return null;
        }

        private static ResearchText.DeedProgress Progress(QuestState state)
        {
            if (state == QuestState.InProgress)
            {
                return ResearchText.DeedProgress.InProgress;
            }

            if (state == QuestState.Completed)
            {
                return ResearchText.DeedProgress.Completed;
            }

            return state == QuestState.Failed
                ? ResearchText.DeedProgress.Failed
                : ResearchText.DeedProgress.NotStarted;
        }

        /// <summary>The wrapper the marker built for the quest it is tracking. Private to the game,
        /// like the screen's own zoom: the marker finds the deed, and asking the quest journal again
        /// every frame would be a search per frame for an answer already sitting there.</summary>
        private static GuiDeed Deed(DeedItem2 marker)
        {
            try
            {
                return TrackedDeed == null ? null : TrackedDeed.GetValue(marker) as GuiDeed;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly FieldInfo TrackedDeed = Tracked(typeof(DeedItem2), "guiDeed");

        private static FieldInfo Tracked(Type type, string name)
        {
            try
            {
                return type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("research: looking up " + type.Name + "." + name + " threw: " + e);
                return null;
            }
        }

        /// <summary>What a technology says about the ones it is joined to, in one part of its readout.
        /// Worked out when the dot is read rather than watched: the wheel's arcs only change when a
        /// technology is researched, and walking a ring is no reason to re-read 162 of them a frame.
        /// </summary>
        private static string Relationships(GuiTechnology2 technology)
        {
            return ResearchText.Relationships(Links(technology));
        }

        /// <summary>
        /// The arcs the wheel draws from this technology to others, said from this end of each of
        /// them.
        ///
        /// Read from the arcs the screen is actually drawing rather than from the link database: the
        /// game decides which of the 162 links apply to this empire and this faction, and it has
        /// already decided it - every arc it drew is one the player can see, and every one it did not
        /// is one that does not exist for them.
        /// </summary>
        private static IList<string> Links(GuiTechnology2 technology)
        {
            List<string> lines = new List<string>();
            try
            {
                TechnologyScreen window = Window();
                AgeTransform container = window == null ? null : window.TechnologyLinksContainer;
                IList<AgeTransform> children = container == null ? null : container.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    // Content: which link arcs contribute a line.
                    if (child == null || !child.Visible)
                    {
                        continue;
                    }

                    TechnologyLinkItem2 link = child.GetComponent<TechnologyLinkItem2>();
                    GuiTechnologyLink2 arc = link == null ? null : link.GuiTechnologyLink;
                    if (arc == null)
                    {
                        continue;
                    }

                    bool source = ReferenceEquals(arc.SourceGuiTechnology, technology);
                    if (!source && !ReferenceEquals(arc.TargetGuiTechnology, technology))
                    {
                        continue;
                    }

                    GuiTechnology2 partner = source
                        ? arc.TargetGuiTechnology
                        : arc.SourceGuiTechnology;
                    string line = ResearchText.Link(
                        Kind(arc),
                        source,
                        partner == null ? null : AgeText.Clean(partner.Title)
                    );
                    if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: reading a technology's links threw: " + e);
            }

            return lines;
        }

        private static ResearchText.LinkKind Kind(GuiTechnologyLink2 arc)
        {
            StaticString category = arc.TechnologyLinkDefinition.SubCategory;
            if (category == TechnologyLinkDefinition.TechnologyLinkTypeExclusion)
            {
                return ResearchText.LinkKind.Exclusion;
            }

            return category == TechnologyLinkDefinition.TechnologyLinkTypeDependency
                ? ResearchText.LinkKind.Dependency
                : ResearchText.LinkKind.CostReduction;
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

        /// <summary>Where the technology is in the queue the game is showing, or -1 for one that is
        /// not in it.</summary>
        private static int QueuePosition(GuiTechnology2 technology)
        {
            try
            {
                TechnologyScreen window = Window();
                List<GuiTechnology2> queue = window == null ? null : window.GuiTechnologiesQueue;
                for (int i = 0; queue != null && i < queue.Count; i++)
                {
                    if (ReferenceEquals(queue[i], technology))
                    {
                        return i;
                    }
                }
            }
            catch (Exception) { }

            return -1;
        }

        // ---- the camera ----

        /// <summary>
        /// Take the viewport where the player asked to look, once whatever it was doing has finished.
        ///
        /// Nothing here fights an animation: a player holding an arrow down walks a whole ring in the
        /// time one zoom takes, and a zoom started per keystroke would leave the view chasing a cursor
        /// that had already gone. The request survives until a frame with no animation running, and
        /// there is only one of it, so the last thing asked for is what is shown.
        /// </summary>
        private void MoveCamera()
        {
            try
            {
                FinishMoveToHead();
                if (_cameraAim == ResearchCamera.Aim.None)
                {
                    return;
                }

                TechnologyScreen window = Window();
                AgeTransform viewport = window == null ? null : window.Viewport;
                AgeModifierSet modifiers = viewport == null ? null : viewport.AgeFirstModifierSet;
                if (modifiers == null || modifiers.ModifiersRunning)
                {
                    return;
                }

                ResearchCamera.Aim aim = _cameraAim;
                TechnologyQuadrantItem quadrant = _cameraQuadrant;
                BaseTechnologyStageItem stage = _cameraStage;
                TechnologyItem2 item = _cameraTechnology;
                Look(ResearchCamera.Aim.None, null, null, null);

                if (aim == ResearchCamera.Aim.Overview)
                {
                    ZoomOut(window);
                }
                else if (aim == ResearchCamera.Aim.Quadrant && quadrant != null)
                {
                    // The game's own answer to a click on the quarter of the wheel: it works out
                    // where the middle of the sector is and zooms or pans there, and it plays the
                    // sound a player at the mouse would hear.
                    ((ITechnologyQuadrantClient)window).OnSectorClick(quadrant);
                }
                else if (aim == ResearchCamera.Aim.Stage && stage != null)
                {
                    ZoomAt(window, StageAim(stage));
                }
                else if (
                    aim == ResearchCamera.Aim.Technology
                    && item != null
                    && !Drawn(item)
                    && ForceZoomIn != null
                )
                {
                    ForceZoomIn.Invoke(window, new object[] { item });
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: moving the camera threw: " + e);
            }
        }

        /// <summary>
        /// Where the middle of a stage's arc is, in the coordinates the screen aims its viewport with.
        ///
        /// The same polar transform the stage uses to place its own dots, with the technology's offset
        /// along the arc left out - so the point is the middle of the ring, at the angle the stage is
        /// centred on. The screen's aim points are measured from the middle of the wheel, which is
        /// where the halving comes from.
        /// </summary>
        private static Vector2 StageAim(BaseTechnologyStageItem stage)
        {
            float radius =
                stage.ViewportNormalizedSize
                * 0.5f
                * (stage.MinRadius + 0.5f * (stage.MaxRadius - stage.MinRadius));
            float angle = Mathf.Deg2Rad * (90f - stage.CenterAngle);
            return new Vector2(radius * Mathf.Cos(angle), -radius * Mathf.Sin(angle));
        }

        /// <summary>Put the aim point in the middle of the screen, the way the screen's own zoom does:
        /// from the overview that means zooming in, and from the close view it means sliding across at
        /// the zoom already in force - which is why the point is scaled by it.</summary>
        private static void ZoomAt(TechnologyScreen window, Vector2 aim)
        {
            GuiValueController zoom =
                window.Viewport == null
                    ? null
                    : window.Viewport.GetComponent<GuiValueController>();
            bool close = zoom != null && zoom.CurrentValue == TechnologyScreen.MaxZoomFactor;
            MethodInfo move = close ? SlideTo : ZoomIn;
            if (move == null)
            {
                return;
            }

            move.Invoke(
                window,
                new object[]
                {
                    close ? aim * TechnologyScreen.MaxZoomFactor : aim,
                    TechnologyScreen.FastTransitionDuration,
                }
            );
        }

        /// <summary>Back to the overview, through the game's own zoom switch - the toggle the key
        /// panel draws, which is also what tells the panel the view has changed.</summary>
        private static void ZoomOut(TechnologyScreen window)
        {
            ResearchKeySidePanel key = window == null ? null : window.ResearchKeySidePanel;
            if (key != null && key.ZoomInToggle != null && key.ZoomInToggle.State)
            {
                AgeWidgets.Toggle(key.ZoomInToggle);
            }
        }

        /// <summary>Whether the renderer is drawing this technology at all - it culls everything
        /// outside the screen, and a dot that is not drawn has no tooltip to show.</summary>
        private static bool Drawn(TechnologyItem2 item)
        {
            try
            {
                return new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height).Overlaps(
                    item.AgeTransform.GetGlobalPosition()
                );
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The screen's own "bring this technology into view", which zooms in from the
        /// overview and pans when it is already zoomed in. Private to the game, like the queue
        /// panel's cancel handler on the star system page.</summary>
        private static readonly MethodInfo ForceZoomIn = Handler(
            typeof(TechnologyScreen),
            "ForceZoomIn"
        );

        /// <summary>The two halves of the screen's own "look here": zoom in on a point from the
        /// overview, slide across to it when the view is already close. Private to the game, and the
        /// only way to aim at anything the game does not itself let the player click on.</summary>
        private static readonly MethodInfo ZoomIn = Handler(typeof(TechnologyScreen), "DoZoomIn");

        private static readonly MethodInfo SlideTo = Handler(
            typeof(TechnologyScreen),
            "DoTranslate"
        );

        private static MethodInfo Handler(Type type, string name)
        {
            try
            {
                return type.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("research: looking up " + type.Name + "." + name + " threw: " + e);
                return null;
            }
        }

        // ---- searching the whole wheel ----

        /// <summary>
        /// Typing looks through every technology the game is willing to draw, wherever it is - which
        /// is the only scope worth having here, because what the player cannot do on this page is
        /// find something.
        ///
        /// Landing on one opens the quadrant and the stage it is in, so the branch the player is put
        /// into is the branch they can then walk. The opening is recorded rather than done: the graph
        /// is rebuilt between this call and the focus landing, and the expansion set belongs to that
        /// rebuild.
        ///
        /// What each dot answers by is its title and then, after a comma, the same words the GAME's
        /// own search box looks through (<see cref="SearchText"/>) - so typing an unlock finds the
        /// technology that grants it, and a title match still outranks it (the search's own
        /// before-the-comma rule).
        /// </summary>
        public override SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            if (focused == null || !Equals(focused.StopKey, TreeStop))
            {
                return null;
            }

            List<TechnologyItem2> items = new List<TechnologyItem2>();
            List<ControlId> quadrants = new List<ControlId>();
            List<ControlId> stages = new List<ControlId>();
            WheelIndex(items, quadrants, stages);

            ExpireSearchText();
            List<TechnologyItem2> found = items;
            List<ControlId> quadrantIds = quadrants;
            List<ControlId> stageIds = stages;
            return new SearchScope(
                found.Count,
                index => SearchText(found[index].GuiTechnology),
                index => Reveal(found[index], quadrantIds[index], stageIds[index]),
                // The dot itself, with nothing opened - so that the shared scope can add what a
                // collapsed dot would declare (each unlock's own dossier) without offering the dot
                // twice (<see cref="SearchScope.Extend"/>).
                index => TechnologyId(found[index])
            );
        }

        /// <summary>What one technology answers a typed search by, built once and kept.</summary>
        private readonly Dictionary<GuiTechnology2, string> _searchText =
            new Dictionary<GuiTechnology2, string>();

        /// <summary>The turn the kept search words were built for. Which unlocks pass their
        /// availability prerequisites is what the words depend on, and that only moves when a
        /// technology finishes - which is a turn boundary.</summary>
        private int _searchTurn = int.MinValue;

        /// <summary>How many times per keystroke the words were composed rather than reused. Read by
        /// the dev probe: the corpus must be built once per technology, never once per letter.
        /// </summary>
        public static int SearchTextBuilds;

        /// <summary>
        /// The words a technology answers a search by: its title, then - after a comma, so a title
        /// match still wins - the technology's own keywords and, for every unlock the player already
        /// passes the availability prerequisites for, that unlock's title, keywords and the localized
        /// titles of its category and sub-category.
        ///
        /// That list is the game's own search corpus, term for term
        /// (<c>TechnologyLookupPanel.BindTechnology</c> :41-73), so typing here finds what typing into
        /// the game's own search box would highlight. Kept per technology because
        /// <see cref="TypeAheadScope"/> is asked again on every keystroke and this walks every unlock
        /// of every dot.
        /// </summary>
        private string SearchText(GuiTechnology2 technology)
        {
            if (technology == null)
            {
                return null;
            }

            string kept;
            if (_searchText.TryGetValue(technology, out kept))
            {
                return kept;
            }

            SearchTextBuilds++;
            string built = BuildSearchText(technology);
            _searchText[technology] = built;
            return built;
        }

        /// <summary>Throw the kept words away when what they were built from can have changed - a new
        /// turn, or a wheel that has been rebound since (its technologies are new objects, which simply
        /// miss the table).</summary>
        private void ExpireSearchText()
        {
            int turn = Turn();
            if (turn != _searchTurn)
            {
                _searchTurn = turn;
                _searchText.Clear();
            }
        }

        private static int Turn()
        {
            try
            {
                Game game = Gui.Game;
                return game == null ? int.MinValue : game.Turn;
            }
            catch (Exception)
            {
                return int.MinValue;
            }
        }

        private static string BuildSearchText(GuiTechnology2 technology)
        {
            MessageBuilder message = new MessageBuilder();
            List<string> terms = new List<string>();
            try
            {
                message.Fragment(AgeText.Clean(technology.Title));
                AddTerms(terms, technology.TechnologyDefinition.GetLocalizedKeywords());
                AddUnlockTerms(terms, technology);
            }
            catch (Exception e)
            {
                Log.Warn("research: building a technology's search words threw: " + e);
            }

            for (int i = 0; i < terms.Count; i++)
            {
                message.ListItemForcedComma(terms[i]);
            }

            return message.Build();
        }

        /// <summary>The words every unlock this technology grants contributes - but only the unlocks
        /// the empire already meets the availability prerequisites for, which is the filter the game's
        /// own search applies, so a search never finds a technology by something it could not yet
        /// give.</summary>
        private static void AddUnlockTerms(List<string> terms, GuiTechnology2 technology)
        {
            IList unlocks = technology.TechnologyUnlocks as IList;
            DepartmentOfScience science = Science();
            Amplitude.Unity.Simulation.SimulationObject empire =
                science == null || science.Empire == null ? null : science.Empire.SimulationObject;
            for (int i = 0; unlocks != null && i < unlocks.Count; i++)
            {
                GuiUnlock unlock = unlocks[i] as GuiUnlock;
                if (unlock == null || unlock.Unlock == null)
                {
                    continue;
                }

                Amplitude.Unity.Framework.IPrerequisiteProvider provider =
                    unlock.Unlock as Amplitude.Unity.Framework.IPrerequisiteProvider;
                if (
                    provider != null
                    && empire != null
                    && !Amplitude.Unity.Framework.PrerequisiteHelper.CheckPrerequisites(
                        empire,
                        provider,
                        ConstructionFlags.UnlockAvailability
                    )
                )
                {
                    continue;
                }

                AddTerm(terms, AgeText.Clean(Localized(unlock.Title)));
                AddTerms(terms, unlock.Unlock.GetLocalizedKeywords());
                AddTerm(terms, TitleOfCategory(unlock.Category));
                AddTerm(terms, TitleOfCategory(unlock.SubCategory));
            }
        }

        private static string Localized(string text)
        {
            try
            {
                return Gui.IsLocalizationKey(text) ? Gui.Localize(text) : text;
            }
            catch (Exception)
            {
                return text;
            }
        }

        private static string TitleOfCategory(string category)
        {
            try
            {
                return string.IsNullOrEmpty(category)
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(category));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddTerms(List<string> terms, IList<string> words)
        {
            for (int i = 0; words != null && i < words.Count; i++)
            {
                AddTerm(terms, words[i]);
            }
        }

        private static void AddTerm(List<string> terms, string word)
        {
            if (string.IsNullOrEmpty(word) || word[0] == '%' || terms.Contains(word))
            {
                return;
            }

            terms.Add(word);
        }

        /// <summary>
        /// Every technology the wheel would draw and the branch each one is buried in, in the order
        /// the wheel lays them out.
        ///
        /// Built on demand rather than kept: it is 107 dots in the fixture and 385 in a full game, and
        /// the only two things that ever want it - a search and a jump from the recommended list - are
        /// both one keypress each.
        /// </summary>
        private void WheelIndex(
            List<TechnologyItem2> items,
            List<ControlId> quadrants,
            List<ControlId> stages
        )
        {
            TechnologyScreen window = Window();
            AgeTransform container = window == null ? null : window.TechnologyQuadrantsContainer;
            IList<AgeTransform> quarters = container == null ? null : container.Children;
            List<TechnologyItem2> drawn = new List<TechnologyItem2>();
            for (int i = 0; quarters != null && i < quarters.Count; i++)
            {
                TechnologyQuadrantItem quadrant =
                    quarters[i].GetComponent<TechnologyQuadrantItem>();
                IList<AgeTransform> stageWidgets =
                    quadrant == null ? null : quadrant.TechnologyStagesContainer.Children;
                for (int j = 0; stageWidgets != null && j < stageWidgets.Count; j++)
                {
                    BaseTechnologyStageItem stage =
                        stageWidgets[j].GetComponent<BaseTechnologyStageItem>();
                    if (stage == null)
                    {
                        continue;
                    }

                    Technologies(stage, drawn);
                    for (int k = 0; k < drawn.Count; k++)
                    {
                        items.Add(drawn[k]);
                        quadrants.Add(QuadrantId(i));
                        stages.Add(StageId(i, j));
                    }
                }
            }
        }

        /// <summary>Open the branch a technology is buried in and answer with the dot itself. The
        /// opening is recorded rather than done: the graph is rebuilt between this call and the focus
        /// landing, and the expansion set belongs to that rebuild.</summary>
        private ControlId Reveal(TechnologyItem2 item, ControlId quadrant, ControlId stage)
        {
            _pendingExpand.Add(quadrant);
            _pendingExpand.Add(stage);
            return TechnologyId(item);
        }

        /// <summary>Open the branches a search landed in. The expansion set is the engine's, and this
        /// is the one moment a screen has anything to say about it.</summary>
        private void ApplyPendingExpansions(GraphBuilder builder)
        {
            if (_pendingExpand.Count == 0)
            {
                return;
            }

            HashSet<ControlId> expansion = builder.Expansion;
            if (expansion != null)
            {
                for (int i = 0; i < _pendingExpand.Count; i++)
                {
                    expansion.Add(_pendingExpand[i]);
                }
            }

            _pendingExpand.Clear();
        }

        // ---- reading the model ----

        /// <summary>The technologies of one stage the game would draw, in the order it draws them:
        /// along the arc, clockwise, which is what the angle each was placed at says.</summary>
        private static void Technologies(BaseTechnologyStageItem stage, List<TechnologyItem2> into)
        {
            into.Clear();
            List<TechnologyItem2> items = stage.TechnologyItems;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && items[i].VisibleByDefinition)
                {
                    into.Add(items[i]);
                }
            }

            into.Sort(ByDrawnAngle);
        }

        /// <summary>Clockwise along the ring. The angle a dot is drawn at is its quadrant's centre
        /// plus a multiple of the position the definition gives it, so ordering by that position is
        /// ordering by angle - and two dots on the same spoke read outermost last.</summary>
        private static readonly Comparison<TechnologyItem2> ByDrawnAngle = (left, right) =>
        {
            int order = left.GuiTechnology.PositionX.CompareTo(right.GuiTechnology.PositionX);
            if (order != 0)
            {
                return order;
            }

            order = left.GuiTechnology.PositionY.CompareTo(right.GuiTechnology.PositionY);
            return order != 0
                ? order
                : string.CompareOrdinal(left.GuiTechnology.Title, right.GuiTechnology.Title);
        };

        /// <summary>A quadrant's name, which is all the wheel writes across it. It used to carry a
        /// researched-over-available count the mod counted for itself; the game draws no such number
        /// anywhere on the wheel, and hearing one in front of every group was noise.</summary>
        private static string QuadrantTitle(TechnologyQuadrantItem quadrant)
        {
            return AgeText.Clean(Gui.Localize(quadrant.QuadrantGuiElement.Title));
        }

        /// <summary>A stage's name, in the game's own full form ("Military I").</summary>
        private static string StageTitle(BaseTechnologyStageItem stage)
        {
            return AgeText.Clean(stage.GuiTechnologyStage.GetFullTitle(null, true));
        }

        /// <summary>While a stage is locked, the game's own sentence about what is holding it -
        /// which it writes on the markers it draws around the ring.</summary>
        private static IList<string> StageLockLines(BaseTechnologyStageItem stage)
        {
            string said = StageLock(stage);
            List<string> lines = new List<string>(1);
            if (!string.IsNullOrEmpty(said))
            {
                lines.Add(said);
            }

            return lines;
        }

        private static string StageLock(BaseTechnologyStageItem stage)
        {
            MessageBuilder message = new MessageBuilder();
            if (stage.Locked)
            {
                // One marker per group of technologies the stage wants researched, and a stage that
                // wants six of the same thing draws six markers saying the same sentence. Said once.
                IList<AgeTransform> markers =
                    stage.UnlockProgressContainer == null
                        ? null
                        : stage.UnlockProgressContainer.Children;
                string last = null;
                for (int i = 0; markers != null && i < markers.Count; i++)
                {
                    TechnologyStageUnlockProgress marker =
                        markers[i].GetComponent<TechnologyStageUnlockProgress>();
                    string text =
                        marker == null || marker.Tooltip == null
                            ? null
                            : AgeText.Clean(marker.Tooltip.Content);
                    if (!string.IsNullOrEmpty(text) && text != last)
                    {
                        message.ListItem(text);
                        last = text;
                    }
                }
            }

            return message.Build();
        }

        /// <summary>What the game says the technology's state is, asked of the game rather than read
        /// off the dot: the dot's own copy is a frame behind whenever an order has just come back,
        /// and it is switched off outright for the frames between queueing something and the screen
        /// refreshing.</summary>
        private static ScienceConstructibleElement.State State(GuiTechnology2 technology)
        {
            DepartmentOfScience science = Science();
            return science == null
                ? ScienceConstructibleElement.State.NotAvailable
                : science.GetTechnologyState(technology.TechnologyDefinition);
        }

        /// <summary>Whether the game would take a click on this technology at all - the same three
        /// states its own toggle is enabled for.</summary>
        private static bool Operable(GuiTechnology2 technology)
        {
            try
            {
                ScienceConstructibleElement.State state = State(technology);
                return state == ScienceConstructibleElement.State.Available
                    || state == ScienceConstructibleElement.State.Queued
                    || state == ScienceConstructibleElement.State.InProgress;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- shared ----

        private static ControlId QuadrantId(int index)
        {
            return ControlId.Structural("research:quadrant/" + index);
        }

        private static ControlId StageId(int quadrant, int stage)
        {
            return ControlId.Structural("research:stage/" + quadrant + "/" + stage);
        }

        /// <summary>
        /// A dot on the wheel, identified by the technology's DEFINITION rather than by the wrapper
        /// the gui hangs on it - which is what the queue's own rows are identified by.
        ///
        /// Two controls that name the same object are the same control as far as the cursor is
        /// concerned: it follows a backing object before it looks at anything else, so sharing one
        /// between the wheel and the queue panel teleported the player into the queue the moment they
        /// queued something.
        /// </summary>
        private static ControlId TechnologyId(TechnologyItem2 item)
        {
            return ControlId.For(
                item.GuiTechnology.TechnologyDefinition,
                "research:technology/" + item.GuiTechnology.Name
            );
        }

        /// <summary>Stop watching a control's parts for change, so it speaks only when the player
        /// reads it or acts on it.</summary>
        private static void Settle(NodeVtable vtable)
        {
            IList<NodeAnnouncement> parts = vtable.Announcements;
            for (int i = 0; parts != null && i < parts.Count; i++)
            {
                if (parts[i] != null)
                {
                    parts[i].Live = false;
                }
            }
        }

        /// <summary>A part the control reports when it is read, rather than one watched for change:
        /// costs and queue positions are worked out from the model every time they are asked for, and
        /// asking every frame would be a page of arithmetic per frame for nothing.</summary>
        private static NodeAnnouncement Value(Func<string> text)
        {
            return new NodeAnnouncement(text, false, AnnouncementKinds.Value);
        }

        /// <summary>The group the game draws a label inside - where it hangs the caption and the
        /// tooltip that go with it.</summary>
        private static AgeTransform Group(AgePrimitiveLabel label)
        {
            AgeTransform widget = Widget(label);
            return widget == null ? null : widget.Parent;
        }

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

        private static string ScreenTitle()
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle("TechnologyScreen"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DepartmentOfScience Science()
        {
            try
            {
                Empire empire = Gui.PlayerEmpire;
                return empire == null ? null : empire.GetAgency<DepartmentOfScience>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static TechnologyScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<TechnologyScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }
    }
}
